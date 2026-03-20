using PaymentGateway.Application.Common.Messaging;
using PaymentGateway.Application.UseCases.Payments.ViewModels;

namespace PaymentGateway.Application.UseCases.Payments.Commands;

public sealed class CreatePaymentCommand : BrighterRequest<CreatePaymentResult>
{
    public Guid MerchantId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BRL";
    public string Method { get; set; } = "credit_card";
    public CustomerDto Customer { get; set; } = null!;
    public string? Description { get; set; }
    public List<PaymentItemDto>? Items { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public string? IdempotencyKey { get; set; }
}
