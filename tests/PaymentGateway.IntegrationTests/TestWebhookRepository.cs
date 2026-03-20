using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.IntegrationTests;

public sealed class TestWebhookRepository : IWebhookRepository
{
    private readonly Dictionary<Guid, Webhook> _webhooks = new();
    private readonly object _lock = new();

    public Task<Webhook?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _webhooks.TryGetValue(id, out var webhook);
            return Task.FromResult<Webhook?>(webhook);
        }
    }

    public Task<IEnumerable<Webhook>> GetAllAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IEnumerable<Webhook>>(_webhooks.Values.ToList());
        }
    }

    public Task<Webhook> AddAsync(Webhook entity, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _webhooks[entity.Id] = entity;
            return Task.FromResult(entity);
        }
    }

    public Task UpdateAsync(Webhook entity, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _webhooks[entity.Id] = entity;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _webhooks.Remove(id);
        }

        return Task.CompletedTask;
    }

    public Task<List<Webhook>> GetByMerchantAsync(Guid merchantId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_webhooks.Values.Where(webhook => webhook.MerchantId == merchantId).ToList());
        }
    }
}
