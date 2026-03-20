using PaymentGateway.Domain.Entities;

namespace PaymentGateway.Domain.Repositories;

public interface IRepository<TEntity> where TEntity : class, IEntity
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
