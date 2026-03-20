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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("NotificationService starting...");

var configuration = BuildConfiguration();
var services = new ServiceCollection();
ConfigureServices(services, configuration);

var sp = services.BuildServiceProvider();
var notifier = sp.GetRequiredService<NotificationService>();

await notifier.StartAsync();

Console.WriteLine("NotificationService started. Press Ctrl+C to stop.");
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    notifier.StopAsync().GetAwaiter().GetResult();
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
    services.AddPaymentGatewayObservability(configuration, "PaymentGateway.Services.Notification");

    var infrastructure = services.AddPaymentGatewayConfiguration(configuration);
    services.AddPaymentGatewayMongoPersistence(
        infrastructure,
        ServiceLifetime.Singleton,
        addWebhookRepository: true);
    services.AddPaymentGatewayDds(
        infrastructure.UseRealDds,
        addSubscriber: true,
        registerPublisherAbstraction: true);
    services.AddPaymentGatewayHttpClient("webhook", TimeSpan.FromSeconds(30));
    services.AddSingleton<WebhookDispatcher>();
    services.AddSingleton<NotificationService>();
}
