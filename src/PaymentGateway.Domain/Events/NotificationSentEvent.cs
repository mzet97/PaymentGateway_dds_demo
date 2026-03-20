namespace PaymentGateway.Domain.Events;

public record NotificationSentEvent : DomainEvent
{
    public Guid PaymentId { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Recipient { get; init; } = string.Empty;
    public bool Success { get; init; }
}
