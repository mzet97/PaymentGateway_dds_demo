namespace PaymentGateway.Application.UseCases.Payments.ViewModels;

public record CreatePaymentResult
{
    public Guid PaymentId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
}
