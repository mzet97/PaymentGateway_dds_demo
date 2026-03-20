using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentGateway.Infrastructure.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgresSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "merchants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    document = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    callback_url = table.Column<string>(type: "text", nullable: true),
                    api_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    max_transaction_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    max_daily_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    max_daily_transactions = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    CustomerEmail = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    CustomerDocument = table.Column<string>(type: "text", nullable: true),
                    CustomerIp = table.Column<string>(type: "text", nullable: true),
                    CustomerPhone = table.Column<string>(type: "text", nullable: true),
                    fraud_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    fraud_decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundedAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    FraudReasons = table.Column<string>(type: "text", nullable: true),
                    MerchantName = table.Column<string>(type: "text", nullable: true),
                    MerchantCategory = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_merchants_api_key",
                table: "merchants",
                column: "api_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_merchants_document",
                table: "merchants",
                column: "document",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_CreatedAt",
                table: "payments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_payments_merchant_id_status_CreatedAt",
                table: "payments",
                columns: new[] { "merchant_id", "status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_status",
                table: "payments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_CreatedAt",
                table: "transactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_payment_id",
                table: "transactions",
                column: "payment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "merchants");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "transactions");
        }
    }
}

