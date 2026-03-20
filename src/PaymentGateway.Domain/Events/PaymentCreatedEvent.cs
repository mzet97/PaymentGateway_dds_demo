namespace PaymentGateway.Domain.Events;

public record PaymentCreatedEvent : DomainEvent
{
    public Guid PaymentId { get; init; }
    public Guid MerchantId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string? IdempotencyKey { get; init; }
}
