using System.Reflection;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Infrastructure.Persistence.Postgres.Entities;

namespace PaymentGateway.Infrastructure.Persistence.Postgres;

public class PaymentDbContext : DbContext
{
    public DbSet<PaymentEntity> Payments { get; set; } = null!;
    public DbSet<MerchantEntity> Merchants { get; set; } = null!;
    public DbSet<TransactionEntity> Transactions { get; set; } = null!;

    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

