using PaymentGateway.Domain.Entities;

namespace PaymentGateway.Domain.Repositories;

public interface IWebhookRepository : IRepository<Webhook>
{
    Task<List<Webhook>> GetByMerchantAsync(Guid merchantId, CancellationToken ct = default);
}
