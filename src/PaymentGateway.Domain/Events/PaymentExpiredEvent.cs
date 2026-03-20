namespace PaymentGateway.Domain.Events;

public record PaymentExpiredEvent : DomainEvent
{
    public Guid PaymentId { get; init; }
}
