namespace PaymentGateway.Domain.Events;

public record PaymentApprovedEvent : DomainEvent
{
    public Guid PaymentId { get; init; }
    public Guid MerchantId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? TransactionId { get; init; }
    public decimal FraudScore { get; init; }
}
