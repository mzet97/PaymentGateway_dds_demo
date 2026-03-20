namespace PaymentGateway.Domain.Events;

public record PaymentRefundedEvent : DomainEvent
{
    public Guid PaymentId { get; init; }
    public Guid RefundId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? Reason { get; init; }
}
