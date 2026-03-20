using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentGateway.Infrastructure.Persistence.Postgres.Entities;

namespace PaymentGateway.Infrastructure.Persistence.Postgres.Mappings;

public class PaymentMapping : IEntityTypeConfiguration<PaymentEntity>
{
    public void Configure(EntityTypeBuilder<PaymentEntity> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.MerchantId)
            .HasColumnName("merchant_id");

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2);

        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Method)
            .HasColumnName("method")
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(x => x.CustomerEmail)
            .HasColumnName("CustomerEmail")
            .IsRequired()
            .HasColumnType("text");

        builder.Property(x => x.CustomerName)
            .HasColumnName("CustomerName")
            .IsRequired()
            .HasColumnType("text");

        builder.Property(x => x.CustomerDocument)
            .HasColumnName("CustomerDocument")
            .HasColumnType("text");

        builder.Property(x => x.CustomerIp)
            .HasColumnName("CustomerIp")
            .HasColumnType("text");

        builder.Property(x => x.CustomerPhone)
            .HasColumnName("CustomerPhone")
            .HasColumnType("text");

        builder.Property(x => x.FraudScore)
            .HasColumnName("fraud_score")
            .HasPrecision(5, 2);

        builder.Property(x => x.FraudDecision)
            .HasColumnName("fraud_decision")
            .HasMaxLength(20);

        builder.Property(x => x.TransactionId)
            .HasColumnName("transaction_id")
            .HasMaxLength(100);

        builder.Property(x => x.IdempotencyKey)
            .HasColumnName("IdempotencyKey")
            .HasColumnType("text");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.Property(x => x.ProcessedAt)
            .HasColumnName("ProcessedAt");

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("ExpiresAt")
            .IsRequired();

        builder.Property(x => x.CapturedAt)
            .HasColumnName("CapturedAt");

        builder.Property(x => x.CancelledAt)
            .HasColumnName("CancelledAt");

        builder.Property(x => x.RefundedAt)
            .HasColumnName("RefundedAt");

        builder.Property(x => x.RefundedAmount)
            .HasColumnName("RefundedAmount");

        builder.Property(x => x.FraudReasons)
            .HasColumnName("FraudReasons")
            .HasColumnType("text");

        builder.Property(x => x.MerchantName)
            .HasColumnName("MerchantName")
            .HasColumnType("text");

        builder.Property(x => x.MerchantCategory)
            .HasColumnName("MerchantCategory")
            .HasColumnType("text");

        builder.HasIndex(x => new { x.MerchantId, x.Status, x.CreatedAt })
            .HasDatabaseName("IX_payments_merchant_id_status_CreatedAt");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_payments_status");

        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("IX_payments_CreatedAt");
    }
}

