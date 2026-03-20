using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.IntegrationTests;

public sealed class TestUnitOfWork : IUnitOfWork
{
    public TestUnitOfWork(IMerchantRepository merchants)
    {
        ArgumentNullException.ThrowIfNull(merchants);
        Merchants = merchants;
    }

    public IMerchantRepository Merchants { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return Task.FromResult(0);
    }

    public Task BeginTransactionAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task CommitAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}
