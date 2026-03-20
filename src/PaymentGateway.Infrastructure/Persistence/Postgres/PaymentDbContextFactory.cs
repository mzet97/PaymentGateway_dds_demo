using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using PaymentGateway.Infrastructure.Persistence.Postgres;

namespace PaymentGateway.Infrastructure.Persistence.Postgres;

public sealed class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    private const string FallbackConnectionString =
        "Host=127.0.0.1;Port=55432;Database=demo-gateway;User Id=app;Password=Admin@123";

    public PaymentDbContext CreateDbContext(string[] args)
    {
        var apiProjectPath = ResolveApiProjectPath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var configuredConnectionString = configuration.GetSection("ConnectionStrings")["DefaultConnection"];
        var connectionString = IsPlaceholder(configuredConnectionString)
            ? FallbackConnectionString
            : configuredConnectionString!;

        var optionsBuilder = new DbContextOptionsBuilder<PaymentDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new PaymentDbContext(optionsBuilder.Options);
    }

    private static string ResolveApiProjectPath()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var candidatePaths = new[]
        {
            Path.Combine(currentDirectory, "src", "PaymentGateway.Api"),
            Path.Combine(currentDirectory, "..", "PaymentGateway.Api"),
            Path.Combine(currentDirectory, "..", "src", "PaymentGateway.Api"),
            Path.Combine(currentDirectory, "..", "..", "PaymentGateway.Api"),
            currentDirectory
        };

        foreach (var candidatePath in candidatePaths)
        {
            if (File.Exists(Path.Combine(candidatePath, "appsettings.json")))
                return Path.GetFullPath(candidatePath);
        }

        return currentDirectory;
    }

    private static bool IsPlaceholder(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               value.Contains("__", StringComparison.Ordinal);
    }
}
