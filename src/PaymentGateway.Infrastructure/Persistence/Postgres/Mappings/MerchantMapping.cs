using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentGateway.Infrastructure.Persistence.Postgres.Entities;

namespace PaymentGateway.Infrastructure.Persistence.Postgres.Mappings;

public class MerchantMapping : IEntityTypeConfiguration<MerchantEntity>
{
    public void Configure(EntityTypeBuilder<MerchantEntity> builder)
    {
        builder.ToTable("merchants");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Document)
            .HasColumnName("document")
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.CallbackUrl)
            .HasColumnName("callback_url")
            .HasColumnType("text");

        builder.Property(x => x.ApiKey)
            .HasColumnName("api_key")
            .HasMaxLength(100);

        builder.Property(x => x.MaxTransactionAmount)
            .HasColumnName("max_transaction_amount")
            .HasPrecision(18, 2);

        builder.Property(x => x.MaxDailyAmount)
            .HasColumnName("max_daily_amount")
            .HasPrecision(18, 2);

        builder.Property(x => x.MaxDailyTransactions)
            .HasColumnName("max_daily_transactions");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.Property(x => x.VerifiedAt)
            .HasColumnName("VerifiedAt");

        builder.HasIndex(x => x.Document)
            .IsUnique()
            .HasDatabaseName("IX_merchants_document");

        builder.HasIndex(x => x.ApiKey)
            .IsUnique()
            .HasDatabaseName("IX_merchants_api_key");
    }
}

