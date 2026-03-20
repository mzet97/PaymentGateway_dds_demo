using PaymentGateway.Application.Common.Messaging;
using PaymentGateway.Application.UseCases.Payments.ViewModels;

namespace PaymentGateway.Application.UseCases.Payments.Commands;

public sealed class CapturePaymentCommand : BrighterRequest<CaptureResult>
{
    public Guid PaymentId { get; set; }
    public decimal? Amount { get; set; }
}
