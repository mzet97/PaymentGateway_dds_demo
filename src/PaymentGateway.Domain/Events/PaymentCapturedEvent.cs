namespace PaymentGateway.Domain.Events;

public record PaymentCapturedEvent : DomainEvent
{
    public Guid PaymentId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
}
