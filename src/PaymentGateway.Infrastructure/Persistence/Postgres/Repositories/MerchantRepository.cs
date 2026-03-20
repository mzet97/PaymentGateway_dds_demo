using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Infrastructure.Persistence.Postgres;
using PaymentGateway.Infrastructure.Persistence.Postgres.Entities;

namespace PaymentGateway.Infrastructure.Persistence.Postgres.Repositories;

public sealed class MerchantRepository : Repository<Merchant, MerchantEntity>, IMerchantRepository
{
    public MerchantRepository(PaymentDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<Merchant?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "get_by_api_key",
            ActivityKind.Internal,
            ("repository", nameof(MerchantRepository)),
            ("store", "postgres"));

        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var entity = await Db.Merchants
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ApiKey == apiKey, ct);

        return entity == null ? null : MapToDomain(entity);
    }

    protected override MerchantEntity MapToStorage(Merchant merchant)
    {
        return new MerchantEntity
        {
            Id = merchant.Id,
            Name = merchant.Name,
            Email = merchant.Email,
            Document = merchant.Document,
            Category = merchant.Category,
            Status = merchant.Status.ToString(),
            CallbackUrl = merchant.CallbackUrl,
            ApiKey = merchant.ApiKey,
            MaxTransactionAmount = merchant.MaxTransactionAmount,
            MaxDailyAmount = merchant.MaxDailyAmount,
            MaxDailyTransactions = merchant.MaxDailyTransactions,
            CreatedAt = merchant.CreatedAt,
            VerifiedAt = merchant.VerifiedAt
        };
    }

    protected override void UpdateStorage(MerchantEntity entity, Merchant merchant)
    {
        entity.Name = merchant.Name;
        entity.Email = merchant.Email;
        entity.Document = merchant.Document;
        entity.Category = merchant.Category;
        entity.CallbackUrl = merchant.CallbackUrl;
        entity.Status = merchant.Status.ToString();
        entity.ApiKey = merchant.ApiKey;
        entity.MaxTransactionAmount = merchant.MaxTransactionAmount;
        entity.MaxDailyAmount = merchant.MaxDailyAmount;
        entity.MaxDailyTransactions = merchant.MaxDailyTransactions;
        entity.VerifiedAt = merchant.VerifiedAt;
    }

    protected override Merchant MapToDomain(MerchantEntity entity)
    {
        return Merchant.Rehydrate(
            entity.Id,
            entity.Name,
            entity.Email,
            entity.Document,
            entity.Category,
            ParseMerchantStatus(entity.Status),
            entity.CallbackUrl,
            entity.ApiKey,
            entity.MaxTransactionAmount,
            entity.MaxDailyAmount,
            entity.MaxDailyTransactions,
            entity.CreatedAt,
            entity.VerifiedAt);
    }

    private static MerchantStatus ParseMerchantStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new InvalidOperationException("Merchant status is required");

        var normalized = status
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (Enum.TryParse<MerchantStatus>(normalized, true, out var parsed))
            return parsed;

        throw new InvalidOperationException($"Unsupported merchant status '{status}'");
    }
}

