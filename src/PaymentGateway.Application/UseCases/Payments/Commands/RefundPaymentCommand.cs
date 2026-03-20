using PaymentGateway.Application.Common.Messaging;
using PaymentGateway.Application.UseCases.Payments.ViewModels;

namespace PaymentGateway.Application.UseCases.Payments.Commands;

public sealed class RefundPaymentCommand : BrighterRequest<RefundResult>
{
    public Guid PaymentId { get; set; }
    public decimal? Amount { get; set; }
    public string? Reason { get; set; }
}
