using PaymentGateway.Infrastructure.Configuration;
using PaymentGateway.Infrastructure.Observability;
using PaymentGateway.Infrastructure.Persistence.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Quartz;

Console.WriteLine("MongoSync Service starting...");

var configuration = BuildConfiguration();
var services = new ServiceCollection();
ConfigureServices(services, configuration);

var sp = services.BuildServiceProvider();

var schedulerFactory = sp.GetRequiredService<ISchedulerFactory>();
var scheduler = await schedulerFactory.GetScheduler();
await scheduler.Start();

Console.WriteLine("MongoSync Service started. Sync job scheduled every 30 seconds.");
Console.WriteLine("Press Ctrl+C to stop.");

await Task.Delay(Timeout.Infinite);

await scheduler.Shutdown();

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
    services.AddPaymentGatewayObservability(configuration, "PaymentGateway.Services.MongoSync");

    var infrastructure = services.AddPaymentGatewayConfiguration(configuration);

    // MongoDB
    services.AddSingleton<IMongoClient>(sp =>
    {
        var connStr = configuration.GetConnectionString("MongoDb")
            ?? configuration["ConnectionStrings:MongoDb"]
            ?? "mongodb://admin:Admin%40123@mongodb.home.arpa:27017/?authSource=admin";
        return new MongoClient(connStr);
    });
    services.AddSingleton<IMongoDatabase>(sp =>
    {
        var client = sp.GetRequiredService<IMongoClient>();
        return client.GetDatabase("payment-gateway");
    });

    // PostgreSQL via EF Core
    var pgConn = configuration.GetConnectionString("DefaultConnection")
        ?? configuration["ConnectionStrings:DefaultConnection"]
        ?? "Host=spsql.home.arpa;Port=5432;Database=demo-gateway;User Id=app;Password=Admin@123;";
    services.AddDbContext<PaymentDbContext>(options => options.UseNpgsql(pgConn), ServiceLifetime.Transient);

    // Quartz scheduling - every 30 seconds for demo
    services.AddQuartz(q =>
    {
        var jobKey = new JobKey("MongoSyncJob");
        q.AddJob<MongoSyncJob>(opts => opts.WithIdentity(jobKey));
        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity("MongoSyncTrigger")
            .StartNow()
            .WithSimpleSchedule(x => x
                .WithInterval(TimeSpan.FromSeconds(30))
                .RepeatForever()));
    });
}
