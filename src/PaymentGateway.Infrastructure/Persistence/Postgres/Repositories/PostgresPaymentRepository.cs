using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Domain.ValueObjects;
using PaymentGateway.Infrastructure.Persistence.Postgres.Entities;

namespace PaymentGateway.Infrastructure.Persistence.Postgres.Repositories;

/// <summary>
/// PostgreSQL implementation of IPaymentRepository for CQRS read-side queries.
/// Reads from the PostgreSQL read model (synced from MongoDB by MongoSync service).
/// </summary>
public class PostgresPaymentRepository : Repository<Payment, PaymentEntity>, IPaymentRepository
{
    public PostgresPaymentRepository(PaymentDbContext dbContext) : base(dbContext)
    {
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
            "repository", "get_by_merchant", ActivityKind.Internal,
            ("repository", "PostgresPaymentRepository"),
            ("store", "postgres"),
            ("merchant_id", merchantId.ToString()));

        var query = BuildReadQuery()
            .Where(p => p.MerchantId == merchantId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);
        if (from.HasValue)
            query = query.Where(p => p.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(p => p.CreatedAt <= to.Value);

        var entities = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip(offset)
            .Take(limit)
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
            "repository", "count_by_merchant", ActivityKind.Internal,
            ("repository", "PostgresPaymentRepository"),
            ("store", "postgres"));

        var query = BuildReadQuery()
            .Where(p => p.MerchantId == merchantId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);
        if (from.HasValue)
            query = query.Where(p => p.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(p => p.CreatedAt <= to.Value);

        return await query.CountAsync(ct);
    }

    protected override Payment MapToDomain(PaymentEntity e)
    {
        return Payment.Rehydrate(
            e.Id,
            e.MerchantId,
            new Money(e.Amount, e.Currency),
            Enum.TryParse<PaymentStatus>(e.Status, true, out var ps) ? ps : PaymentStatus.Pending,
            Enum.TryParse<PaymentMethod>(e.Method.Replace("_", ""), true, out var pm) ? pm : PaymentMethod.CreditCard,
            new CustomerInfo(
                e.CustomerEmail ?? "",
                e.CustomerName ?? "",
                e.CustomerDocument,
                e.CustomerIp,
                e.CustomerPhone),
            e.Description,
            null, // items
            null, // metadata
            e.FraudScore.HasValue
                ? new FraudCheckResult(
                    e.FraudScore.Value,
                    Enum.TryParse<FraudDecision>(e.FraudDecision, true, out var fd)
                        ? fd : FraudDecision.Review)
                : null,
            e.TransactionId,
            e.IdempotencyKey,
            e.CreatedAt,
            null, // updatedAt
            e.ProcessedAt,
            e.ExpiresAt,
            e.CapturedAt,
            e.CancelledAt,
            e.RefundedAt,
            e.RefundedAmount.HasValue ? new Money(e.RefundedAmount.Value, e.Currency) : null);
    }

    protected override PaymentEntity MapToStorage(Payment p)
    {
        return new PaymentEntity
        {
            Id = p.Id,
            MerchantId = p.MerchantId,
            Amount = p.Amount.Amount,
            Currency = p.Amount.Currency,
            Status = p.Status.ToString().ToLowerInvariant(),
            Method = p.Method.ToString().ToLowerInvariant(),
            Description = p.Description,
            CustomerEmail = p.Customer.Email,
            CustomerName = p.Customer.Name,
            CustomerDocument = p.Customer.Document,
            CustomerIp = p.Customer.Ip,
            CustomerPhone = p.Customer.Phone,
            FraudScore = p.FraudResult?.RiskScore,
            FraudDecision = p.FraudResult?.Decision.ToString(),
            TransactionId = p.TransactionId,
            CreatedAt = p.CreatedAt,
            ProcessedAt = p.ProcessedAt,
            ExpiresAt = p.ExpiresAt,
            CapturedAt = p.CapturedAt,
            RefundedAt = p.RefundedAt,
            RefundedAmount = p.RefundedAmount?.Amount,
        };
    }

    protected override void UpdateStorage(PaymentEntity storage, Payment domain)
    {
        storage.Status = domain.Status.ToString().ToLowerInvariant();
        storage.FraudScore = domain.FraudResult?.RiskScore;
        storage.FraudDecision = domain.FraudResult?.Decision.ToString();
        storage.ProcessedAt = domain.ProcessedAt;
        storage.TransactionId = domain.TransactionId;
        storage.CapturedAt = domain.CapturedAt;
        storage.RefundedAt = domain.RefundedAt;
        storage.RefundedAmount = domain.RefundedAmount?.Amount;
    }
}
