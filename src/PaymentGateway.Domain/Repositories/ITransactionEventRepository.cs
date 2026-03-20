namespace PaymentGateway.Domain.Repositories;

public sealed class StoredTransactionEvent
{
    public Guid Id { get; init; }
    public Guid PaymentId { get; init; }
    public Guid MerchantId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = "{}";
    public DateTime OccurredAt { get; init; }
}

public interface ITransactionEventRepository
{
    Task StoreAsync(StoredTransactionEvent transactionEvent, CancellationToken ct = default);
    Task<List<StoredTransactionEvent>> GetByPaymentAsync(Guid paymentId, CancellationToken ct = default);
    Task<List<StoredTransactionEvent>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);
}
