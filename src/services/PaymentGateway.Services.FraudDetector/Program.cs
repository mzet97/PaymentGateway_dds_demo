using System.Diagnostics;
using System.Text.Json;
using PaymentGateway.Application.Services;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Infrastructure.Configuration;
using PaymentGateway.Infrastructure.DDS;
using PaymentGateway.Infrastructure.DDS.DdsTypes;
using PaymentGateway.Infrastructure.Observability;
using PaymentGateway.Infrastructure.Services;
using PaymentGateway.Domain.ValueObjects;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

Console.WriteLine("FraudDetector Service starting...");

var configuration = BuildConfiguration();
var services = new ServiceCollection();
ConfigureServices(services, configuration);

var sp = services.BuildServiceProvider();
var detector = sp.GetRequiredService<FraudDetector>();

await detector.StartAsync();

Console.WriteLine("FraudDetector Service started. Press Ctrl+C to stop.");
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    detector.StopAsync().GetAwaiter().GetResult();
};

await Task.Delay(Timeout.Infinite);

static IConfiguration BuildConfiguration()
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

static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    var openRouterOptions = configuration
        .GetSection(OpenRouterOptions.SectionName)
        .Get<OpenRouterOptions>() ?? new OpenRouterOptions();

    var openRouterApiKey = ConfigurationGuard.Require(
        openRouterOptions.ApiKey,
        $"{OpenRouterOptions.SectionName}:{nameof(OpenRouterOptions.ApiKey)}");
    var openRouterModel = openRouterOptions.Model;

    services.AddPaymentGatewayObservability(configuration, "PaymentGateway.Services.FraudDetector");

    var infrastructure = services.AddPaymentGatewayConfiguration(configuration);
    services.AddPaymentGatewayMongoPersistence(
        infrastructure,
        ServiceLifetime.Singleton,
        addPaymentRepository: true);
    services.AddPaymentGatewayRedisCaching(
        infrastructure.RedisConnection,
        infrastructure.RedisInstanceName,
        ServiceLifetime.Singleton);
    services.AddPaymentGatewayDds(
        infrastructure.UseRealDds,
        addSubscriber: true,
        registerPublisherAbstraction: true);
    services.AddHttpClient("openrouter", client =>
    {
        if (Uri.TryCreate(openRouterOptions.BaseUrl, UriKind.Absolute, out var openRouterBaseUri))
        {
            client.BaseAddress = openRouterBaseUri;
        }

        client.Timeout = TimeSpan.FromSeconds(30);
    });

    services.AddSingleton<IOpenRouterFraudService>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<OpenRouterFraudService>>();
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var cache = sp.GetService<IRedisService>();

        return new OpenRouterFraudService(
            httpClientFactory.CreateClient("openrouter"),
            logger,
            openRouterApiKey,
            openRouterModel,
            cache);
    });

    services.AddSingleton<FraudDetector>();
}
