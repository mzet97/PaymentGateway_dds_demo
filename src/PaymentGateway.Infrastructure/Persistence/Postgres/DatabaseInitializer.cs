using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using PaymentGateway.Infrastructure.Persistence.Postgres;
using PaymentGateway.Infrastructure.Persistence.Postgres.Entities;

namespace PaymentGateway.Infrastructure.Persistence.Postgres;

public static class DatabaseInitializer
{
    private const string InitialMigrationId = "20260307023435_InitialPostgresSchema";
    private const string EfProductVersion = "8.0.0";

    public static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        if (ShouldSkipInitialization(services))
            return;

        var logger = services.GetRequiredService<ILogger<PaymentDbContext>>();

        try
        {
            var context = services.GetRequiredService<PaymentDbContext>();

            logger.LogInformation("Starting PaymentGateway database migration.");
            await BaselineLegacySchemaAsync(context, logger, ct);
            await context.Database.MigrateAsync(ct);
            logger.LogInformation("PaymentGateway database migration completed.");

            await SeedDemoDataAsync(context, services, logger, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing the PaymentGateway database.");
            throw;
        }
    }

    private static async Task BaselineLegacySchemaAsync(
        PaymentDbContext context,
        ILogger logger,
        CancellationToken ct)
    {
        if (!await TableExistsAsync(context, "merchants", ct))
            return;

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
            """,
            ct);

        if (await MigrationExistsAsync(context, InitialMigrationId, ct))
            return;

        logger.LogWarning(
            "Legacy PaymentGateway schema detected without EF migration history. Applying baseline for migration {MigrationId}.",
            InitialMigrationId);

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS payments (
                id uuid NOT NULL,
                merchant_id uuid NOT NULL,
                amount numeric(18,2) NOT NULL,
                currency character varying(3) NOT NULL,
                status character varying(20) NOT NULL,
                method character varying(20) NOT NULL,
                description text NULL,
                "CustomerEmail" text NOT NULL,
                "CustomerName" text NOT NULL,
                "CustomerDocument" text NULL,
                "CustomerIp" text NULL,
                "CustomerPhone" text NULL,
                fraud_score numeric(5,2) NULL,
                fraud_decision character varying(20) NULL,
                transaction_id character varying(100) NULL,
                "IdempotencyKey" text NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "ProcessedAt" timestamp with time zone NULL,
                "ExpiresAt" timestamp with time zone NOT NULL,
                "CapturedAt" timestamp with time zone NULL,
                "CancelledAt" timestamp with time zone NULL,
                "RefundedAt" timestamp with time zone NULL,
                "RefundedAmount" numeric NULL,
                "FraudReasons" text NULL,
                "MerchantName" text NULL,
                "MerchantCategory" text NULL,
                CONSTRAINT "PK_payments" PRIMARY KEY (id)
            );

            CREATE TABLE IF NOT EXISTS transactions (
                id uuid NOT NULL,
                payment_id uuid NOT NULL,
                type character varying(20) NOT NULL,
                amount numeric(18,2) NOT NULL,
                currency character varying(3) NOT NULL,
                reference character varying(100) NULL,
                reason text NULL,
                success boolean NOT NULL,
                error_code character varying(20) NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_transactions" PRIMARY KEY (id)
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_merchants_api_key" ON merchants (api_key);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_merchants_document" ON merchants (document);
            CREATE INDEX IF NOT EXISTS "IX_payments_CreatedAt" ON payments ("CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_payments_merchant_id_status_CreatedAt" ON payments (merchant_id, status, "CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_payments_status" ON payments (status);
            CREATE INDEX IF NOT EXISTS "IX_transactions_CreatedAt" ON transactions ("CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_transactions_payment_id" ON transactions (payment_id);
            """,
            ct);

        await context.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('{InitialMigrationId}', '{EfProductVersion}')
            ON CONFLICT ("MigrationId") DO NOTHING;
            """,
            ct);
    }

    private static bool ShouldSkipInitialization(IServiceProvider services)
    {
        var skipInitializer = Environment.GetEnvironmentVariable("SKIP_DB_INITIALIZER");
        if (string.Equals(skipInitializer, "true", StringComparison.OrdinalIgnoreCase))
            return true;

        var configuration = services.GetService<IConfiguration>();
        if (configuration != null &&
            bool.TryParse(configuration["PaymentGateway:SkipDatabaseInitialization"], out var skipFromConfiguration) &&
            skipFromConfiguration)
        {
            return true;
        }

        var hostEnvironment = services.GetService<IHostEnvironment>();
        return hostEnvironment?.IsEnvironment("Testing") == true;
    }

    private static async Task SeedDemoDataAsync(
        PaymentDbContext context,
        IServiceProvider services,
        ILogger logger,
        CancellationToken ct)
    {
        if (!ShouldSeedDemoData(services))
            return;

        var demoMerchants = BuildDemoMerchants();
        foreach (var demoMerchant in demoMerchants)
        {
            var existing = await context.Merchants.FirstOrDefaultAsync(m => m.Id == demoMerchant.Id, ct);
            if (existing == null)
            {
                context.Merchants.Add(demoMerchant);
                continue;
            }

            existing.Name = demoMerchant.Name;
            existing.Email = demoMerchant.Email;
            existing.Document = demoMerchant.Document;
            existing.Category = demoMerchant.Category;
            existing.Status = demoMerchant.Status;
            existing.CallbackUrl = demoMerchant.CallbackUrl;
            existing.ApiKey = demoMerchant.ApiKey;
            existing.MaxTransactionAmount = demoMerchant.MaxTransactionAmount;
            existing.MaxDailyAmount = demoMerchant.MaxDailyAmount;
            existing.MaxDailyTransactions = demoMerchant.MaxDailyTransactions;
            existing.VerifiedAt = demoMerchant.VerifiedAt;
        }

        await context.SaveChangesAsync(ct);
        logger.LogInformation("PaymentGateway demo merchants ensured successfully.");
    }

    private static bool ShouldSeedDemoData(IServiceProvider services)
    {
        var explicitSetting = Environment.GetEnvironmentVariable("PAYMENT_GATEWAY_SEED_DEMO_DATA");
        if (bool.TryParse(explicitSetting, out var shouldSeed))
            return shouldSeed;

        var configuration = services.GetService<IConfiguration>();
        if (configuration != null &&
            bool.TryParse(configuration["PaymentGateway:SeedDemoData"], out var shouldSeedFromConfiguration))
        {
            return shouldSeedFromConfiguration;
        }

        var hostEnvironment = services.GetService<IHostEnvironment>();
        return hostEnvironment?.IsDevelopment() == true;
    }

    private static IReadOnlyList<MerchantEntity> BuildDemoMerchants()
    {
        var now = DateTime.UtcNow;

        return
        [
            new MerchantEntity
            {
                Id = ReadGuid("PAYMENT_GATEWAY_DEMO_MERCHANT_ID", "11111111-1111-1111-1111-111111111111"),
                Name = Environment.GetEnvironmentVariable("PAYMENT_GATEWAY_DEMO_MERCHANT_NAME") ?? "Smoke Merchant",
                Email = Environment.GetEnvironmentVariable("PAYMENT_GATEWAY_DEMO_MERCHANT_EMAIL") ?? "smoke-merchant@example.test",
                Document = Environment.GetEnvironmentVariable("PAYMENT_GATEWAY_DEMO_MERCHANT_DOCUMENT") ?? "12345678901",
                Category = Environment.GetEnvironmentVariable("PAYMENT_GATEWAY_DEMO_MERCHANT_CATEGORY") ?? "services",
                Status = "Active",
                CallbackUrl = Environment.GetEnvironmentVariable("PAYMENT_GATEWAY_DEMO_MERCHANT_CALLBACK_URL") ?? "https://merchant-smoke.example.test/webhook",
                ApiKey = Environment.GetEnvironmentVariable("PAYMENT_GATEWAY_DEMO_MERCHANT_API_KEY") ?? "sk_test_smoke_merchant",
                MaxTransactionAmount = 1000000.00m,
                MaxDailyAmount = 10000000.00m,
                MaxDailyTransactions = 100000,
                CreatedAt = now,
                VerifiedAt = now
            },
            new MerchantEntity
            {
                Id = ReadGuid("PAYMENT_GATEWAY_DEMO_OTHER_MERCHANT_ID", "22222222-2222-2222-2222-222222222222"),
                Name = Environment.GetEnvironmentVariable("PAYMENT_GATEWAY_DEMO_OTHER_MERCHANT_NAME") ?? "Smoke Other Merchant",
                Email = Environment.GetEnvironmentVariable("PAYMENT_GATEWAY_DEMO_OTHER_MERCHANT_EMAIL") ?? "smoke-other@example.test",
                Document = Environment.GetEnvironmentVariable("PAYMENT_GATEWAY_DEMO_OTHER_MERCHANT_DOCUMENT") ?? "10987654321",
                Category = Environment.GetEnvironmentVariable("PAYMENT_GATEWAY_DEMO_OTHER_MERCHANT_CATEGORY") ?? "retail",
                Status = "Active",
                CallbackUrl = Environment.GetEnvironmentVariable("PAYMENT_GATEWAY_DEMO_OTHER_MERCHANT_CALLBACK_URL") ?? "https://merchant-other.example.test/webhook",
                ApiKey = Environment.GetEnvironmentVariable("PAYMENT_GATEWAY_DEMO_OTHER_MERCHANT_API_KEY") ?? "sk_test_smoke_other",
                MaxTransactionAmount = 1000000.00m,
                MaxDailyAmount = 10000000.00m,
                MaxDailyTransactions = 100000,
                CreatedAt = now,
                VerifiedAt = now
            }
        ];
    }

    private static Guid ReadGuid(string environmentVariable, string fallback)
    {
        var rawValue = Environment.GetEnvironmentVariable(environmentVariable);
        if (Guid.TryParse(rawValue, out var parsed) && parsed != Guid.Empty)
            return parsed;

        return Guid.Parse(fallback);
    }

    private static async Task<bool> TableExistsAsync(
        PaymentDbContext context,
        string tableName,
        CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();
        var openedHere = false;

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
            openedHere = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = @tableName
                );
                """;

            var parameter = new NpgsqlParameter("@tableName", tableName);
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(ct);
            return result is bool exists && exists;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> MigrationExistsAsync(
        PaymentDbContext context,
        string migrationId,
        CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();
        var openedHere = false;

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
            openedHere = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM "__EFMigrationsHistory"
                    WHERE "MigrationId" = @migrationId
                );
                """;

            var parameter = new NpgsqlParameter("@migrationId", migrationId);
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(ct);
            return result is bool exists && exists;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
}

