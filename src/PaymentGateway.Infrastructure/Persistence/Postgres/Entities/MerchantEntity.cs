namespace PaymentGateway.Infrastructure.Persistence.Postgres.Entities;

public class MerchantEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Document { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = "pending_verification";
    public string? CallbackUrl { get; set; }
    public string? ApiKey { get; set; }
    public decimal MaxTransactionAmount { get; set; } = 10000m;
    public decimal MaxDailyAmount { get; set; } = 100000m;
    public int MaxDailyTransactions { get; set; } = 1000;
    public DateTime CreatedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
}

