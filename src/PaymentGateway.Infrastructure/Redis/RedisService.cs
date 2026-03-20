using System.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Services;

namespace PaymentGateway.Infrastructure.Redis;

public sealed class RedisService : IRedisService
{
    private static readonly TimeSpan DefaultAbsoluteExpiration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DefaultSlidingExpiration = TimeSpan.FromMinutes(10);

    private readonly IDistributedCache _cache;

    public RedisService(IDistributedCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    public async Task SetValueAsync(
        string key,
        string value,
        TimeSpan? absoluteExpirationRelativeToNow = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "cache",
            "redis_set",
            ActivityKind.Client,
            ("cache.key_category", ResolveKeyCategory(key)));

        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow ?? DefaultAbsoluteExpiration,
            SlidingExpiration = slidingExpiration ?? DefaultSlidingExpiration
        };

        await _cache.SetStringAsync(key, value, options, ct);
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "cache",
            "redis_get",
            ActivityKind.Client,
            ("cache.key_category", ResolveKeyCategory(key)));

        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return await _cache.GetStringAsync(key, ct);
    }

    public async Task RemoveValueAsync(string key, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "cache",
            "redis_remove",
            ActivityKind.Client,
            ("cache.key_category", ResolveKeyCategory(key)));

        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await _cache.RemoveAsync(key, ct);
    }

    private static string ResolveKeyCategory(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "unknown";

        var delimiterIndex = key.IndexOf(':');
        return delimiterIndex <= 0 ? key : key[..delimiterIndex];
    }
}
