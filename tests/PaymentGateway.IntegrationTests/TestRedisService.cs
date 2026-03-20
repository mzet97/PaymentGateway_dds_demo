using System.Collections.Concurrent;
using PaymentGateway.Domain.Services;

namespace PaymentGateway.IntegrationTests;

public class TestRedisService : IRedisService
{
    private readonly ConcurrentDictionary<string, (string Value, DateTime? ExpiresAt)> _store = new();

    public Task SetValueAsync(
        string key,
        string value,
        TimeSpan? absoluteExpirationRelativeToNow = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken ct = default)
    {
        var expiresAt = absoluteExpirationRelativeToNow.HasValue
            ? DateTime.UtcNow.Add(absoluteExpirationRelativeToNow.Value)
            : (DateTime?)null;

        _store[key] = (value, expiresAt);
        return Task.CompletedTask;
    }

    public Task<string?> GetValueAsync(string key, CancellationToken ct = default)
    {
        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt.HasValue && DateTime.UtcNow > entry.ExpiresAt.Value)
            {
                _store.TryRemove(key, out _);
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult<string?>(entry.Value);
        }

        return Task.FromResult<string?>(null);
    }

    public Task RemoveValueAsync(string key, CancellationToken ct = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
