using PaymentGateway.Domain.Entities;

namespace PaymentGateway.Domain.Repositories;

public interface IMerchantRepository : IRepository<Merchant>
{
    Task<Merchant?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default);
}
