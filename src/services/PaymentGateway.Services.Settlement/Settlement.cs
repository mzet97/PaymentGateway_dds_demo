namespace PaymentGateway.Services.Settlement;

public class Settlement
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal SettlementAmount { get; set; }
    public SettlementStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
