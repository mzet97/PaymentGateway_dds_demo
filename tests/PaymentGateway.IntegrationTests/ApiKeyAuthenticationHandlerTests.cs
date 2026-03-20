using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentGateway.Api.Middlewares;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Domain.Services;
using PaymentGateway.Infrastructure.Redis;
using Xunit;

namespace PaymentGateway.IntegrationTests;

public class ApiKeyAuthenticationHandlerTests
{
    [Fact]
    public async Task Authenticate_WhenCacheMissAndRepositoryHit_ShouldSucceedAndCache()
    {
        var merchant = Merchant.Create("Auth Merchant", "auth@example.test", "12345678901", "services");
        merchant.Activate();
        var apiKey = merchant.ApiKey!;

        var cache = new InMemoryRedisService();
        var repository = new TestMerchantRepository(merchant);
        var handler = CreateHandler(cache, new TestUnitOfWork(repository));
        var context = new DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = apiKey;

        await handler.InitializeAsync(
            new AuthenticationScheme("ApiKey", "ApiKey", typeof(ApiKeyAuthenticationHandler)),
            context);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        (await cache.GetAsync<string>($"apikey:{apiKey}")).Should().Be(merchant.Id.ToString());
    }

    [Fact]
    public async Task Authenticate_WhenApiKeyIsUnknown_ShouldFail()
    {
        var cache = new InMemoryRedisService();
        var repository = new TestMerchantRepository();
        var handler = CreateHandler(cache, new TestUnitOfWork(repository));
        var context = new DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = "pk_invalid";

        await handler.InitializeAsync(
            new AuthenticationScheme("ApiKey", "ApiKey", typeof(ApiKeyAuthenticationHandler)),
            context);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
    }

    private static ApiKeyAuthenticationHandler CreateHandler(IRedisService cache, IUnitOfWork unitOfWork)
    {
        return new ApiKeyAuthenticationHandler(
            new StaticOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
            LoggerFactory.Create(_ => { }),
            UrlEncoder.Default,
            cache,
            unitOfWork);
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T> where T : class
    {
        private readonly T _value;

        public StaticOptionsMonitor(T value)
        {
            _value = value;
        }

        public T CurrentValue => _value;

        public T Get(string? name) => _value;

        public IDisposable OnChange(Action<T, string?> listener) => NoopDisposable.Instance;
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();
        public void Dispose() { }
    }

    private sealed class InMemoryRedisService : IRedisService
    {
        private readonly Dictionary<string, (string Value, DateTimeOffset ExpiresAt)> _store = new();

        public Task SetValueAsync(
            string key,
            string value,
            TimeSpan? absoluteExpirationRelativeToNow = null,
            TimeSpan? slidingExpiration = null,
            CancellationToken ct = default)
        {
            var expiresAt = DateTimeOffset.UtcNow.Add(absoluteExpirationRelativeToNow ?? TimeSpan.FromMinutes(10));
            _store[key] = (value, expiresAt);
            return Task.CompletedTask;
        }

        public Task<string?> GetValueAsync(string key, CancellationToken ct = default)
        {
            if (_store.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return Task.FromResult<string?>(entry.Value);
            }

            return Task.FromResult<string?>(null);
        }

        public Task RemoveValueAsync(string key, CancellationToken ct = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }
    }
}
