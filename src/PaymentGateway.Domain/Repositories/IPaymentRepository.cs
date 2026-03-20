using PaymentGateway.Domain.Entities;

namespace PaymentGateway.Domain.Repositories;

public interface IPaymentRepository : IRepository<Payment>
{
    Task<List<Payment>> GetByMerchantAsync(
        Guid merchantId,
        string? status,
        DateTime? from,
        DateTime? to,
        int limit,
        int offset,
        CancellationToken ct = default);
    Task<int> CountByMerchantAsync(
        Guid merchantId,
        string? status,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default);
}
