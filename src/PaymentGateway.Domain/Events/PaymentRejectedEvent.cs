namespace PaymentGateway.Domain.Events;

public record PaymentRejectedEvent : DomainEvent
{
    public Guid PaymentId { get; init; }
    public Guid MerchantId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public decimal FraudScore { get; init; }
}
