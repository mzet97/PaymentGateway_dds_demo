using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.Infrastructure.Persistence.Postgres.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly PaymentDbContext _context;
    private IMerchantRepository? _merchantRepository;

    public UnitOfWork(PaymentDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public IMerchantRepository Merchants => _merchantRepository ??= new MerchantRepository(_context);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_context.Database.CurrentTransaction == null)
        {
            await _context.Database.BeginTransactionAsync(ct);
        }
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            await _context.SaveChangesAsync(ct);
            await _context.Database.CommitTransactionAsync(ct);
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            await _context.Database.RollbackTransactionAsync(ct);
        }
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

