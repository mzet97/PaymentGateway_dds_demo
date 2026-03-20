namespace PaymentGateway.Infrastructure.Persistence.Postgres.Entities;

public class TransactionEntity
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BRL";
    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

