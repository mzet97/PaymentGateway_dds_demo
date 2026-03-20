using Paramore.Brighter;
using PaymentGateway.Application.Common.Behaviours;
using PaymentGateway.Application.Services;
using PaymentGateway.Application.UseCases.Payments.Commands;
using PaymentGateway.Application.UseCases.Payments.ViewModels;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.Application.UseCases.Payments.Commands.Handlers;

public sealed class RefundPaymentCommandHandler : RequestHandlerAsync<RefundPaymentCommand>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IDdsPublisher _ddsPublisher;

    public RefundPaymentCommandHandler(IPaymentRepository paymentRepository, IDdsPublisher ddsPublisher)
    {
        _paymentRepository = paymentRepository;
        _ddsPublisher = ddsPublisher;
    }

    [RequestLogging(0, HandlerTiming.Before)]
    [RequestValidation(1, HandlerTiming.Before)]
    public override async Task<RefundPaymentCommand> HandleAsync(
        RefundPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var payment = await _paymentRepository.GetByIdAsync(command.PaymentId, cancellationToken);
        if (payment is null)
            throw new InvalidOperationException("Payment not found");

        payment.Refund(command.Amount);
        await _paymentRepository.UpdateAsync(payment, cancellationToken);

        var refundId = Guid.NewGuid();

        await _ddsPublisher.PublishAsync("payment.refunded", new
        {
            refundId,
            paymentId = payment.Id,
            amount = command.Amount ?? payment.Amount.Amount,
            currency = payment.Amount.Currency,
            reason = command.Reason,
            timestamp = DateTime.UtcNow
        }, cancellationToken);

        PaymentGatewayTelemetry.RecordPaymentLifecycle(
            "refunded",
            payment.Status.ToString().ToLowerInvariant(),
            payment.Method.ToString(),
            payment.MerchantId);

        command.Result = new RefundResult
        {
            RefundId = refundId,
            PaymentId = payment.Id,
            Amount = command.Amount ?? payment.Amount.Amount,
            Status = "refunded",
            CreatedAt = DateTime.UtcNow
        };

        return await base.HandleAsync(command, cancellationToken);
    }
}
