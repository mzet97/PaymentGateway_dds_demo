namespace PaymentGateway.Domain.Events;

public record FraudCheckedEvent : DomainEvent
{
    public Guid PaymentId { get; init; }
    public decimal RiskScore { get; init; }
    public string Decision { get; init; } = string.Empty;
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
    public string? Model { get; init; }
    public long LatencyMs { get; init; }
}
