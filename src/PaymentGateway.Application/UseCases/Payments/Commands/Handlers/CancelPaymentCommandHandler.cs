using Paramore.Brighter;
using PaymentGateway.Application.Common.Behaviours;
using PaymentGateway.Application.Services;
using PaymentGateway.Application.UseCases.Payments.Commands;
using PaymentGateway.Application.UseCases.Payments.ViewModels;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.Application.UseCases.Payments.Commands.Handlers;

public sealed class CancelPaymentCommandHandler : RequestHandlerAsync<CancelPaymentCommand>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IDdsPublisher _ddsPublisher;

    public CancelPaymentCommandHandler(IPaymentRepository paymentRepository, IDdsPublisher ddsPublisher)
    {
        _paymentRepository = paymentRepository;
        _ddsPublisher = ddsPublisher;
    }

    [RequestLogging(0, HandlerTiming.Before)]
    [RequestValidation(1, HandlerTiming.Before)]
    public override async Task<CancelPaymentCommand> HandleAsync(
        CancelPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var payment = await _paymentRepository.GetByIdAsync(command.PaymentId, cancellationToken);
        if (payment is null)
            throw new InvalidOperationException("Payment not found");

        payment.Cancel();
        await _paymentRepository.UpdateAsync(payment, cancellationToken);

        await _ddsPublisher.PublishAsync("payment.cancelled", new
        {
            paymentId = payment.Id,
            merchantId = payment.MerchantId,
            cancelledAt = payment.CancelledAt ?? DateTime.UtcNow,
            reason = command.Reason
        }, cancellationToken);

        PaymentGatewayTelemetry.RecordPaymentLifecycle(
            "cancelled",
            payment.Status.ToString().ToLowerInvariant(),
            payment.Method.ToString(),
            payment.MerchantId);

        command.Result = new CaptureResult
        {
            PaymentId = payment.Id,
            Status = "cancelled",
            CapturedAt = payment.CancelledAt ?? DateTime.UtcNow
        };

        return await base.HandleAsync(command, cancellationToken);
    }
}
