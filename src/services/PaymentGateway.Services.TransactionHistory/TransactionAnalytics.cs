namespace PaymentGateway.Services.TransactionHistory;

public class TransactionAnalytics
{
    public int TotalTransactions { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AverageAmount { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public Dictionary<string, int> ByMethod { get; set; } = new();
}
