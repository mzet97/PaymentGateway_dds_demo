namespace PaymentGateway.Domain.Events;

public record PaymentCancelledEvent : DomainEvent
{
    public Guid PaymentId { get; init; }
    public string? Reason { get; init; }
}
