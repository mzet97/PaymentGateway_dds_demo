using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaymentGateway.Application.Services;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Infrastructure.Configuration;
using PaymentGateway.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;

namespace PaymentGateway.Services.Settlement;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("SettlementService starting...");

        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);

        var serviceProvider = services.BuildServiceProvider();

        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("SettlementService started");

        var settlementService = serviceProvider.GetRequiredService<SettlementService>();

        // Run daily settlement
        await settlementService.ProcessDailySettlementsAsync();

        Console.WriteLine("SettlementService completed");
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
        services.AddPaymentGatewayObservability(configuration, "PaymentGateway.Services.Settlement");

        var infrastructure = services.AddPaymentGatewayConfiguration(configuration);
        services.AddPaymentGatewayMongoPersistence(
            infrastructure,
            ServiceLifetime.Singleton,
            addPaymentRepository: true);
        services.AddPaymentGatewayDds(
            infrastructure.UseRealDds,
            addSubscriber: false,
            registerPublisherAbstraction: true);
        services.AddSingleton<SettlementService>();
    }
}
