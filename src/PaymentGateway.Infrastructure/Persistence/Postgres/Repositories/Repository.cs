using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.Infrastructure.Persistence.Postgres.Repositories;

public abstract class Repository<TEntity, TStorageEntity> : IRepository<TEntity>
    where TEntity : class, IEntity
    where TStorageEntity : class
{
    protected readonly PaymentDbContext Db;
    protected readonly DbSet<TStorageEntity> DbSet;

    protected Repository(PaymentDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        Db = dbContext;
        DbSet = dbContext.Set<TStorageEntity>();
    }

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "get_by_id",
            ActivityKind.Internal,
            ("repository", GetType().Name),
            ("store", "postgres"),
            ("entity.id", id.ToString()));

        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));

        var storageEntity = await BuildReadQuery()
            .FirstOrDefaultAsync(entity => EF.Property<Guid>(entity, nameof(IEntity.Id)) == id, ct);

        return storageEntity == null ? null : MapToDomain(storageEntity);
    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "get_all",
            ActivityKind.Internal,
            ("repository", GetType().Name),
            ("store", "postgres"));

        var entities = await BuildReadQuery().ToListAsync(ct);
        return entities.Select(MapToDomain).ToList();
    }

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "add",
            ActivityKind.Internal,
            ("repository", GetType().Name),
            ("store", "postgres"),
            ("entity.id", entity?.Id.ToString()));

        ArgumentNullException.ThrowIfNull(entity);

        var storageEntity = MapToStorage(entity);
        DbSet.Add(storageEntity);
        await Db.SaveChangesAsync(ct);

        return entity;
    }

    public virtual async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "update",
            ActivityKind.Internal,
            ("repository", GetType().Name),
            ("store", "postgres"),
            ("entity.id", entity?.Id.ToString()));

        ArgumentNullException.ThrowIfNull(entity);

        var storageEntity = await DbSet.FindAsync(new object[] { entity.Id }, ct);
        if (storageEntity == null)
            throw new InvalidOperationException($"{typeof(TEntity).Name} not found.");

        UpdateStorage(storageEntity, entity);
        await Db.SaveChangesAsync(ct);
    }

    public virtual async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "repository",
            "delete",
            ActivityKind.Internal,
            ("repository", GetType().Name),
            ("store", "postgres"),
            ("entity.id", id.ToString()));

        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));

        var storageEntity = await DbSet.FindAsync(new object[] { id }, ct);
        if (storageEntity == null)
            throw new InvalidOperationException($"{typeof(TEntity).Name} not found.");

        DbSet.Remove(storageEntity);
        await Db.SaveChangesAsync(ct);
    }

    protected virtual IQueryable<TStorageEntity> BuildReadQuery()
    {
        return DbSet.AsNoTracking().AsQueryable();
    }

    protected abstract TEntity MapToDomain(TStorageEntity storageEntity);

    protected abstract TStorageEntity MapToStorage(TEntity entity);

    protected abstract void UpdateStorage(TStorageEntity storageEntity, TEntity entity);
}

