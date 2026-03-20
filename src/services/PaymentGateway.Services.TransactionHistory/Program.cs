using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Infrastructure.Configuration;
using PaymentGateway.Infrastructure.DDS;
using PaymentGateway.Infrastructure.DDS.DdsTypes;
using PaymentGateway.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace PaymentGateway.Services.TransactionHistory;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("TransactionHistoryService starting...");

        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);

        var serviceProvider = services.BuildServiceProvider();

        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("TransactionHistoryService started");

        var historyService = serviceProvider.GetRequiredService<TransactionHistoryService>();

        // Subscribe to events
        await historyService.SubscribeToEventsAsync();

        // Keep running
        await Task.Delay(Timeout.Infinite);
    }

    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddPaymentGatewayObservability(configuration, "PaymentGateway.Services.TransactionHistory");

        var infrastructure = services.AddPaymentGatewayConfiguration(configuration);
        services.AddPaymentGatewayMongoPersistence(
            infrastructure,
            ServiceLifetime.Singleton,
            addTransactionEventRepository: true);
        services.AddPaymentGatewayDds(
            infrastructure.UseRealDds,
            addSubscriber: true,
            registerFallbackSubscriber: true);
        services.AddSingleton<TransactionHistoryService>();
    }
}
