namespace PaymentGateway.Domain.Services;

public interface IRedisService
{
    Task SetValueAsync(
        string key,
        string value,
        TimeSpan? absoluteExpirationRelativeToNow = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken ct = default);

    Task<string?> GetValueAsync(string key, CancellationToken ct = default);

    Task RemoveValueAsync(string key, CancellationToken ct = default);
}
