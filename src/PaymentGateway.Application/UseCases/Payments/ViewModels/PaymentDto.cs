namespace PaymentGateway.Application.UseCases.Payments.ViewModels;

public record PaymentDto
{
    public Guid Id { get; init; }
    public Guid MerchantId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public CustomerDto? Customer { get; init; }
    public List<PaymentItemDto> Items { get; init; } = new();
    public decimal? FraudScore { get; init; }
    public string? FraudDecision { get; init; }
    public string? TransactionId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
    public DateTime? CapturedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public DateTime? RefundedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public decimal? RefundedAmount { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
