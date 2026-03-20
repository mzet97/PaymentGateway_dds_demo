using System.Diagnostics;
using MongoDB.Driver;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Domain.ValueObjects;
using PaymentGateway.Infrastructure.Persistence;
using PaymentGateway.Infrastructure.Persistence.Mongo.Entities;

namespace PaymentGateway.Infrastructure.Persistence.Mongo.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly IMongoDbContext _mongoContext;

    public PaymentRepository(IMongoDbContext mongoContext)
    {
        ArgumentNullException.ThrowIfNull(mongoContext);
        _mongoContext = mongoContext;
    }

    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "get_by_id",
            ActivityKind.Internal,
            ("repository", nameof(PaymentRepository)),
            ("store", "mongodb"),
            ("entity.id", id.ToString()));

        if (id == Guid.Empty)
            throw new ArgumentException("Payment id cannot be empty.", nameof(id));

        var entity = await _mongoContext.PendingPayments
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync(ct);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IEnumerable<Payment>> GetAllAsync(CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "get_all",
            ActivityKind.Internal,
            ("repository", nameof(PaymentRepository)),
            ("store", "mongodb"));

        var entities = await _mongoContext.PendingPayments
            .Find(Builders<PendingPaymentEntity>.Filter.Empty)
            .SortByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<Payment> AddAsync(Payment payment, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "add",
            ActivityKind.Internal,
            ("repository", nameof(PaymentRepository)),
            ("store", "mongodb"),
            ("entity.id", payment?.Id.ToString()));

        ArgumentNullException.ThrowIfNull(payment);

        var entity = MapToEntity(payment);
        await _mongoContext.PendingPayments.InsertOneAsync(entity, cancellationToken: ct);
        return payment;
    }

    public async Task UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "update",
            ActivityKind.Internal,
            ("repository", nameof(PaymentRepository)),
            ("store", "mongodb"),
            ("entity.id", payment?.Id.ToString()));

        ArgumentNullException.ThrowIfNull(payment);

        var entity = MapToEntity(payment);
        await _mongoContext.PendingPayments.ReplaceOneAsync(
            p => p.Id == payment.Id,
            entity,
            cancellationToken: ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "delete",
            ActivityKind.Internal,
            ("repository", nameof(PaymentRepository)),
            ("store", "mongodb"),
            ("entity.id", id.ToString()));

        if (id == Guid.Empty)
            throw new ArgumentException("Payment id cannot be empty.", nameof(id));

        await _mongoContext.PendingPayments.DeleteOneAsync(p => p.Id == id, ct);
    }

    public async Task<List<Payment>> GetByMerchantAsync(
        Guid merchantId,
        string? status,
        DateTime? from,
        DateTime? to,
        int limit,
        int offset,
        CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "get_by_merchant",
            ActivityKind.Internal,
            ("repository", nameof(PaymentRepository)),
            ("store", "mongodb"),
            ("merchant.id", merchantId == Guid.Empty ? "all" : merchantId.ToString()));

        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");

        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative.");

        var filter = BuildMerchantFilter(merchantId, status, from, to);

        var entities = await _mongoContext.PendingPayments
            .Find(filter)
            .SortByDescending(p => p.CreatedAt)
            .Skip(offset)
            .Limit(limit)
            .ToListAsync(ct);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> CountByMerchantAsync(
        Guid merchantId,
        string? status,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "count_by_merchant",
            ActivityKind.Internal,
            ("repository", nameof(PaymentRepository)),
            ("store", "mongodb"),
            ("merchant.id", merchantId == Guid.Empty ? "all" : merchantId.ToString()));

        var filter = BuildMerchantFilter(merchantId, status, from, to);

        return (int)await _mongoContext.PendingPayments.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    private static FilterDefinition<PendingPaymentEntity> BuildMerchantFilter(
        Guid merchantId,
        string? status,
        DateTime? from,
        DateTime? to)
    {
        var filterBuilder = Builders<PendingPaymentEntity>.Filter;
        var filter = filterBuilder.Empty;

        if (merchantId != Guid.Empty)
            filter &= filterBuilder.Eq(p => p.MerchantId, merchantId);

        if (!string.IsNullOrWhiteSpace(status))
            filter &= filterBuilder.Eq(p => p.Status, status);

        if (from.HasValue)
            filter &= filterBuilder.Gte(p => p.CreatedAt, from.Value);

        if (to.HasValue)
            filter &= filterBuilder.Lte(p => p.CreatedAt, to.Value);

        return filter;
    }

    private static Payment MapToDomain(PendingPaymentEntity entity)
    {
        var fraudResult = entity.FraudScore.HasValue && !string.IsNullOrWhiteSpace(entity.FraudDecision)
            ? new FraudCheckResult(entity.FraudScore.Value, ParseFraudDecision(entity.FraudDecision))
            : null;

        var refundedAmount = entity.RefundedAmount.HasValue
            ? new Money(entity.RefundedAmount.Value, entity.Currency)
            : (Money?)null;

        return Payment.Rehydrate(
            entity.Id,
            entity.MerchantId,
            new Money(entity.Amount, entity.Currency),
            ParsePaymentStatus(entity.Status),
            ParsePaymentMethod(entity.Method),
            new CustomerInfo(
                entity.Customer.Email,
                entity.Customer.Name,
                entity.Customer.Document,
                entity.Customer.Ip,
                entity.Customer.Phone),
            entity.Description,
            entity.Items.Select(i => new PaymentItem(i.Sku, i.Name, i.Quantity, i.UnitPrice, entity.Currency)),
            entity.Metadata,
            fraudResult,
            entity.TransactionId,
            entity.IdempotencyKey,
            entity.CreatedAt,
            entity.ProcessedAt,
            entity.ProcessedAt,
            entity.ExpiresAt,
            entity.CapturedAt,
            null,
            entity.RefundedAt,
            refundedAmount);
    }

    private static PaymentMethod ParsePaymentMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
            throw new InvalidOperationException("Payment method is required");

        var normalized = method
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (Enum.TryParse<PaymentMethod>(normalized, true, out var parsed))
            return parsed;

        throw new InvalidOperationException($"Unsupported payment method '{method}'");
    }

    private static PaymentStatus ParsePaymentStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new InvalidOperationException("Payment status is required");

        var normalized = status
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (Enum.TryParse<PaymentStatus>(normalized, true, out var parsed))
            return parsed;

        throw new InvalidOperationException($"Unsupported payment status '{status}'");
    }

    private static FraudDecision ParseFraudDecision(string? decision)
    {
        if (string.IsNullOrWhiteSpace(decision))
            throw new InvalidOperationException("Fraud decision is required");

        var normalized = decision
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (Enum.TryParse<FraudDecision>(normalized, true, out var parsed))
            return parsed;

        throw new InvalidOperationException($"Unsupported fraud decision '{decision}'");
    }

    private static PendingPaymentEntity MapToEntity(Payment payment)
    {
        return new PendingPaymentEntity
        {
            Id = payment.Id,
            MerchantId = payment.MerchantId,
            Amount = payment.Amount.Amount,
            Currency = payment.Amount.Currency,
            Status = payment.Status.ToString().ToLowerInvariant(),
            Method = payment.Method.ToString().ToLowerInvariant(),
            Customer = new CustomerDocument
            {
                Email = payment.Customer.Email,
                Name = payment.Customer.Name,
                Document = payment.Customer.Document,
                Ip = payment.Customer.Ip,
                Phone = payment.Customer.Phone
            },
            Description = payment.Description,
            Items = payment.Items.Select(i => new PaymentItemDocument
            {
                Sku = i.Sku,
                Name = i.Name,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice.Amount
            }).ToList(),
            Metadata = payment.Metadata.ToDictionary(kv => kv.Key, kv => kv.Value),
            FraudScore = payment.FraudResult?.RiskScore,
            FraudDecision = payment.FraudResult?.Decision.ToString(),
            TransactionId = payment.TransactionId,
            IdempotencyKey = payment.IdempotencyKey,
            CreatedAt = payment.CreatedAt,
            ProcessedAt = payment.ProcessedAt,
            ExpiresAt = payment.ExpiresAt,
            CapturedAt = payment.CapturedAt,
            RefundedAt = payment.RefundedAt,
            RefundedAmount = payment.RefundedAmount?.Amount
        };
    }
}
