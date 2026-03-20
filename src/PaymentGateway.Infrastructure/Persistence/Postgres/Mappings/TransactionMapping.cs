using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentGateway.Infrastructure.Persistence.Postgres.Entities;

namespace PaymentGateway.Infrastructure.Persistence.Postgres.Mappings;

public class TransactionMapping : IEntityTypeConfiguration<TransactionEntity>
{
    public void Configure(EntityTypeBuilder<TransactionEntity> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.PaymentId)
            .HasColumnName("payment_id");

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2);

        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Reference)
            .HasColumnName("reference")
            .HasMaxLength(100);

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasColumnType("text");

        builder.Property(x => x.Success)
            .HasColumnName("success");

        builder.Property(x => x.ErrorCode)
            .HasColumnName("error_code")
            .HasMaxLength(20);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.HasIndex(x => x.PaymentId)
            .HasDatabaseName("IX_transactions_payment_id");

        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("IX_transactions_CreatedAt");
    }
}

