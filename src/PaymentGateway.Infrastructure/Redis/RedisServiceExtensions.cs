using System.Text.Json;
using PaymentGateway.Domain.Services;

namespace PaymentGateway.Infrastructure.Redis;

public static class RedisServiceExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<T?> GetAsync<T>(this IRedisService redisService, string key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(redisService);

        var value = await redisService.GetValueAsync(key, ct);
        if (string.IsNullOrWhiteSpace(value))
            return default;

        return JsonSerializer.Deserialize<T>(value, JsonOptions);
    }

    public static Task SetAsync<T>(
        this IRedisService redisService,
        string key,
        T value,
        TimeSpan absoluteExpirationRelativeToNow,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(redisService);

        var serialized = JsonSerializer.Serialize(value, JsonOptions);
        return redisService.SetValueAsync(key, serialized, absoluteExpirationRelativeToNow, null, ct);
    }
}
