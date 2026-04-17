using PaymentGateway.Domain.Enums;

namespace PaymentGateway.Domain.ValueObjects;

public sealed record FraudCheckResult
{
    public decimal RiskScore { get; }
    public FraudDecision Decision { get; }
    public IReadOnlyList<string> Reasons { get; }
    public FraudMetadata? Metadata { get; }
    public DateTime CheckedAt { get; }

    public FraudCheckResult(
        decimal riskScore,
        FraudDecision decision,
        IReadOnlyList<string>? reasons = null,
        FraudMetadata? metadata = null)
    {
        if (riskScore < 0 || riskScore > 100)
            throw new ArgumentOutOfRangeException(nameof(riskScore), "Risk score must be between 0 and 100");

        RiskScore = riskScore;
        Decision = decision;
        Reasons = reasons ?? Array.Empty<string>();
        Metadata = metadata;
        CheckedAt = DateTime.UtcNow;
    }

    public static FraudCheckResult Default => new(
        50m,
        FraudDecision.Review,
        new List<string> { "Default score - no analysis performed" });

    public static FraudCheckResult Approved(decimal score, FraudMetadata? metadata = null) => new(
        score,
        FraudDecision.Approved,
        Array.Empty<string>(),
        metadata);

    public static FraudCheckResult Review(decimal score, IReadOnlyList<string> reasons, FraudMetadata? metadata = null) => new(
        score,
        FraudDecision.Review,
        reasons,
        metadata);

    public static FraudCheckResult Rejected(decimal score, IReadOnlyList<string> reasons, FraudMetadata? metadata = null) => new(
        score,
        FraudDecision.Rejected,
        reasons,
        metadata);
}

public sealed record FraudMetadata
{
    public string Model { get; }
    public long LatencyMs { get; }
    public decimal Confidence { get; }
    public DateTime AnalyzedAt { get; }

    public FraudMetadata(string model, long latencyMs, decimal confidence)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model is required", nameof(model));

        if (latencyMs < 0)
            throw new ArgumentOutOfRangeException(nameof(latencyMs), "Latency must be non-negative");

        if (confidence < 0 || confidence > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1");

        Model = model;
        LatencyMs = latencyMs;
        Confidence = confidence;
        AnalyzedAt = DateTime.UtcNow;
    }
}
