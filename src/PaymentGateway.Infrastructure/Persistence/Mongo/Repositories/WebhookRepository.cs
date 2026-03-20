using System.Diagnostics;
using MongoDB.Driver;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Infrastructure.Persistence;
using PaymentGateway.Infrastructure.Persistence.Mongo.Entities;

namespace PaymentGateway.Infrastructure.Persistence.Mongo.Repositories;

public class WebhookRepository : IWebhookRepository
{
    private readonly IMongoDbContext _mongoContext;

    public WebhookRepository(IMongoDbContext mongoContext)
    {
        ArgumentNullException.ThrowIfNull(mongoContext);
        _mongoContext = mongoContext;
    }

    public async Task<Webhook?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "get_webhook_by_id",
            ActivityKind.Internal,
            ("repository", nameof(WebhookRepository)),
            ("store", "mongodb"),
            ("entity.id", id.ToString()));

        if (id == Guid.Empty)
            throw new ArgumentException("Webhook id cannot be empty.", nameof(id));

        var entity = await _mongoContext.Webhooks
            .Find(w => w.Id == id)
            .FirstOrDefaultAsync(ct);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IEnumerable<Webhook>> GetAllAsync(CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "get_all_webhooks",
            ActivityKind.Internal,
            ("repository", nameof(WebhookRepository)),
            ("store", "mongodb"));

        var entities = await _mongoContext.Webhooks
            .Find(Builders<WebhookEntity>.Filter.Empty)
            .ToListAsync(ct);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<List<Webhook>> GetByMerchantAsync(Guid merchantId, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "get_webhooks_by_merchant",
            ActivityKind.Internal,
            ("repository", nameof(WebhookRepository)),
            ("store", "mongodb"),
            ("merchant.id", merchantId.ToString()));

        if (merchantId == Guid.Empty)
            throw new ArgumentException("Merchant id cannot be empty.", nameof(merchantId));

        var entities = await _mongoContext.Webhooks
            .Find(w => w.MerchantId == merchantId)
            .ToListAsync(ct);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<Webhook> AddAsync(Webhook webhook, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "add_webhook",
            ActivityKind.Internal,
            ("repository", nameof(WebhookRepository)),
            ("store", "mongodb"),
            ("entity.id", webhook?.Id.ToString()));

        ArgumentNullException.ThrowIfNull(webhook);

        var entity = MapToEntity(webhook);
        await _mongoContext.Webhooks.InsertOneAsync(entity, cancellationToken: ct);
        return webhook;
    }

    public async Task UpdateAsync(Webhook webhook, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "update_webhook",
            ActivityKind.Internal,
            ("repository", nameof(WebhookRepository)),
            ("store", "mongodb"),
            ("entity.id", webhook?.Id.ToString()));

        ArgumentNullException.ThrowIfNull(webhook);

        var entity = MapToEntity(webhook);
        await _mongoContext.Webhooks.ReplaceOneAsync(
            w => w.Id == webhook.Id,
            entity,
            cancellationToken: ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "delete_webhook",
            ActivityKind.Internal,
            ("repository", nameof(WebhookRepository)),
            ("store", "mongodb"),
            ("entity.id", id.ToString()));

        if (id == Guid.Empty)
            throw new ArgumentException("Webhook id cannot be empty.", nameof(id));

        await _mongoContext.Webhooks.DeleteOneAsync(w => w.Id == id, ct);
    }

    private static Webhook MapToDomain(WebhookEntity entity)
    {
        return Webhook.Rehydrate(
            entity.Id,
            entity.MerchantId,
            entity.Url,
            entity.Events,
            entity.Secret,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }

    private static WebhookEntity MapToEntity(Webhook webhook)
    {
        // Use reflection to set the Id since the Create method generates a new one
        var entity = new WebhookEntity
        {
            Id = webhook.Id,
            MerchantId = webhook.MerchantId,
            Url = webhook.Url,
            Events = webhook.Events,
            Secret = webhook.Secret,
            IsActive = webhook.IsActive,
            CreatedAt = webhook.CreatedAt,
            UpdatedAt = webhook.UpdatedAt
        };
        return entity;
    }
}
