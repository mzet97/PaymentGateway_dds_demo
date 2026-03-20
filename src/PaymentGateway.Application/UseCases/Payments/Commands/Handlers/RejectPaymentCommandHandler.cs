using Paramore.Brighter;
using PaymentGateway.Application.Common.Behaviours;
using PaymentGateway.Application.Services;
using PaymentGateway.Application.UseCases.Payments.Commands;
using PaymentGateway.Application.UseCases.Payments.ViewModels;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.Application.UseCases.Payments.Commands.Handlers;

public sealed class RejectPaymentCommandHandler : RequestHandlerAsync<RejectPaymentCommand>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IDdsPublisher _ddsPublisher;

    public RejectPaymentCommandHandler(IPaymentRepository paymentRepository, IDdsPublisher ddsPublisher)
    {
        _paymentRepository = paymentRepository;
        _ddsPublisher = ddsPublisher;
    }

    [RequestLogging(0, HandlerTiming.Before)]
    [RequestValidation(1, HandlerTiming.Before)]
    public override async Task<RejectPaymentCommand> HandleAsync(
        RejectPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var payment = await _paymentRepository.GetByIdAsync(command.PaymentId, cancellationToken);
        if (payment is null)
            throw new InvalidOperationException("Payment not found");

        payment.Reject(command.Reason);
        await _paymentRepository.UpdateAsync(payment, cancellationToken);

        await _ddsPublisher.PublishAsync("payment.rejected", new
        {
            paymentId = payment.Id,
            merchantId = payment.MerchantId,
            reason = command.Reason,
            timestamp = DateTime.UtcNow
        }, cancellationToken);

        PaymentGatewayTelemetry.RecordPaymentLifecycle(
            "rejected",
            payment.Status.ToString().ToLowerInvariant(),
            payment.Method.ToString(),
            payment.MerchantId);

        command.Result = new CaptureResult
        {
            PaymentId = payment.Id,
            Status = "rejected",
            CapturedAt = payment.ProcessedAt ?? DateTime.UtcNow
        };

        return await base.HandleAsync(command, cancellationToken);
    }
}
