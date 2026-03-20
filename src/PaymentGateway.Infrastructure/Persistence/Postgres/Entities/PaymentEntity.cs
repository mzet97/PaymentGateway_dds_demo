namespace PaymentGateway.Infrastructure.Persistence.Postgres.Entities;

public class PaymentEntity
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BRL";
    public string Status { get; set; } = "pending";
    public string Method { get; set; } = "credit_card";
    public string? Description { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerDocument { get; set; }
    public string? CustomerIp { get; set; }
    public string? CustomerPhone { get; set; }
    public decimal? FraudScore { get; set; }
    public string? FraudDecision { get; set; }
    public string? TransactionId { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? CapturedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public decimal? RefundedAmount { get; set; }
    public string? FraudReasons { get; set; }
    public string? MerchantName { get; set; }
    public string? MerchantCategory { get; set; }
}

