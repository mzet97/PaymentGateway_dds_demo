namespace PaymentGateway.Application.UseCases.Payments.ViewModels;

public record RefundResult
{
    public Guid RefundId { get; init; }
    public Guid PaymentId { get; init; }
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
