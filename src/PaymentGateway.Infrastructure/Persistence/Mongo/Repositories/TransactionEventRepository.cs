using System.Diagnostics;
using MongoDB.Driver;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Infrastructure.Persistence.Mongo.Entities;

namespace PaymentGateway.Infrastructure.Persistence.Mongo.Repositories;

public class TransactionEventRepository : ITransactionEventRepository
{
    private readonly IMongoCollection<TransactionEventEntity> _events;

    public TransactionEventRepository(IMongoDbContext mongoContext)
    {
        ArgumentNullException.ThrowIfNull(mongoContext);
        _events = mongoContext.TransactionEvents;
    }

    public async Task StoreAsync(StoredTransactionEvent transactionEvent, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "store_transaction_event",
            ActivityKind.Internal,
            ("repository", nameof(TransactionEventRepository)),
            ("store", "mongodb"),
            ("payment.id", transactionEvent?.PaymentId.ToString()));

        ArgumentNullException.ThrowIfNull(transactionEvent);

        var entity = new TransactionEventEntity
        {
            Id = transactionEvent.Id == Guid.Empty ? Guid.NewGuid() : transactionEvent.Id,
            PaymentId = transactionEvent.PaymentId,
            MerchantId = transactionEvent.MerchantId,
            EventType = transactionEvent.EventType,
            PayloadJson = transactionEvent.PayloadJson,
            OccurredAt = transactionEvent.OccurredAt == default ? DateTime.UtcNow : transactionEvent.OccurredAt
        };

        await _events.InsertOneAsync(entity, cancellationToken: ct);
    }

    public async Task<List<StoredTransactionEvent>> GetByPaymentAsync(Guid paymentId, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "get_events_by_payment",
            ActivityKind.Internal,
            ("repository", nameof(TransactionEventRepository)),
            ("store", "mongodb"),
            ("payment.id", paymentId.ToString()));

        if (paymentId == Guid.Empty)
            throw new ArgumentException("Payment id cannot be empty.", nameof(paymentId));

        var entities = await _events
            .Find(e => e.PaymentId == paymentId)
            .SortBy(e => e.OccurredAt)
            .ToListAsync(ct);

        return entities.Select(Map).ToList();
    }

    public async Task<List<StoredTransactionEvent>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "get_events_by_date_range",
            ActivityKind.Internal,
            ("repository", nameof(TransactionEventRepository)),
            ("store", "mongodb"));

        if (from > to)
            throw new ArgumentException("The 'from' date must be earlier than or equal to 'to'.", nameof(from));

        var entities = await _events
            .Find(e => e.OccurredAt >= from && e.OccurredAt <= to)
            .SortBy(e => e.OccurredAt)
            .ToListAsync(ct);

        return entities.Select(Map).ToList();
    }

    private static StoredTransactionEvent Map(TransactionEventEntity entity)
    {
        return new StoredTransactionEvent
        {
            Id = entity.Id,
            PaymentId = entity.PaymentId,
            MerchantId = entity.MerchantId,
            EventType = entity.EventType,
            PayloadJson = entity.PayloadJson,
            OccurredAt = entity.OccurredAt
        };
    }
}
