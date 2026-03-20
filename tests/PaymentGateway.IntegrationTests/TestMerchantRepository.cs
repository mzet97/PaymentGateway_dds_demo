using PaymentGateway.Domain.Repositories;
using PaymentGateway.Domain.Entities;

namespace PaymentGateway.IntegrationTests;

public sealed class TestMerchantRepository : IMerchantRepository
{
    private readonly Dictionary<Guid, Merchant> _merchants = new();
    private readonly object _lock = new();

    public TestMerchantRepository(params Merchant[] merchants)
    {
        var defaultMerchant = CreateActiveMerchant(
            IntegrationTestDefaults.DefaultMerchantId,
            "pk_test_default_merchant");
        _merchants[defaultMerchant.Id] = defaultMerchant;

        foreach (var merchant in merchants)
        {
            _merchants[merchant.Id] = merchant;
        }
    }

    public Task<Merchant?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return Task.FromResult<Merchant?>(null);
        }

        lock (_lock)
        {
            if (_merchants.TryGetValue(id, out var existing))
            {
                return Task.FromResult<Merchant?>(existing);
            }

            var merchant = CreateActiveMerchant(id, $"pk_test_{id:N}");
            _merchants[id] = merchant;
            return Task.FromResult<Merchant?>(merchant);
        }
    }

    public Task<Merchant?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return Task.FromResult<Merchant?>(null);

        lock (_lock)
        {
            var merchant = _merchants.Values.FirstOrDefault(m => m.ApiKey == apiKey);
            return Task.FromResult<Merchant?>(merchant);
        }
    }

    public Task<IEnumerable<Merchant>> GetAllAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IEnumerable<Merchant>>(_merchants.Values.ToList());
        }
    }

    public Task<Merchant> AddAsync(Merchant merchant, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (merchant.Status == MerchantStatus.PendingVerification)
            {
                merchant.Activate();
            }

            _merchants[merchant.Id] = merchant;
            return Task.FromResult(merchant);
        }
    }

    public Task UpdateAsync(Merchant merchant, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _merchants[merchant.Id] = merchant;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _merchants.Remove(id);
        }

        return Task.CompletedTask;
    }

    private static Merchant CreateActiveMerchant(Guid id, string apiKey)
    {
        return Merchant.Rehydrate(
            id,
            "Integration Merchant",
            "integration@example.test",
            "12345678901",
            "test",
            MerchantStatus.Active,
            "https://example.test/webhook",
            apiKey,
            10000m,
            100000m,
            1000,
            DateTime.UtcNow,
            DateTime.UtcNow);
    }
}
