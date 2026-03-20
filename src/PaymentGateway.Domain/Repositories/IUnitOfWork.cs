namespace PaymentGateway.Domain.Repositories;

public interface IUnitOfWork : IDisposable
{
    IMerchantRepository Merchants { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
