namespace PaymentGateway.Services.Settlement;

public enum SettlementStatus
{
    Pending,
    Processing,
    Processed,
    Failed,
    Cancelled
}
