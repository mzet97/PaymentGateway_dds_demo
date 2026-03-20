using PaymentGateway.Application.Common.Messaging;
using PaymentGateway.Application.UseCases.Payments.ViewModels;

namespace PaymentGateway.Application.UseCases.Payments.Commands;

public sealed class CancelPaymentCommand : BrighterRequest<CaptureResult>
{
    public Guid PaymentId { get; set; }
    public string? Reason { get; set; }
}
