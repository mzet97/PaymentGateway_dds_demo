using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaymentGateway.Application.Services;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Domain.Services;
using PaymentGateway.Infrastructure.DDS;
using PaymentGateway.Infrastructure.Persistence.Mongo;
using PaymentGateway.Infrastructure.Persistence.Mongo.Repositories;
using PaymentGateway.Infrastructure.Redis;
using PaymentGateway.Infrastructure.Persistence.Postgres;
using StackExchange.Redis;

namespace PaymentGateway.Infrastructure.Configuration;

public static class ServiceCollectionExtensions
{
    public static InfrastructureRegistration AddPaymentGatewayInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var registration = services.AddPaymentGatewayConfiguration(configuration);

        services.AddPaymentGatewayMongoPersistence(
            registration,
            ServiceLifetime.Scoped,
            addPaymentRepository: true, // MongoDB for writes (payments created via DDS) and reads
            addWebhookRepository: true,
            addTransactionEventRepository: true);
        services.AddPaymentGatewayPostgresPersistence(
            registration.PostgresConnection,
            useForPaymentQueries: false); // MongoSync copies to PostgreSQL for analytics/reporting
        services.AddPaymentGatewayRedisCaching(
            registration.RedisConnection,
            registration.RedisInstanceName,
            ServiceLifetime.Scoped);
        services.AddPaymentGatewayDds(registration.UseRealDds, addSubscriber: false);
        services.AddPaymentGatewayHttpClient("webhook", TimeSpan.FromSeconds(30));

        return registration;
    }

    public static InfrastructureRegistration AddPaymentGatewayConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ConnectionStringsOptions>(
            configuration.GetSection(ConnectionStringsOptions.SectionName));
        services.Configure<RedisOptions>(
            configuration.GetSection(RedisOptions.SectionName));
        services.Configure<OpenRouterOptions>(
            configuration.GetSection(OpenRouterOptions.SectionName));
        services.Configure<MinioOptions>(
            configuration.GetSection(MinioOptions.SectionName));
        services.Configure<DdsOptions>(
            configuration.GetSection(DdsOptions.SectionName));

        var connectionStrings = configuration
            .GetSection(ConnectionStringsOptions.SectionName)
            .Get<ConnectionStringsOptions>() ?? new ConnectionStringsOptions();

        var redisOptions = configuration
            .GetSection(RedisOptions.SectionName)
            .Get<RedisOptions>() ?? new RedisOptions();

        var ddsOptions = configuration
            .GetSection(DdsOptions.SectionName)
            .Get<DdsOptions>() ?? new DdsOptions();

        var mongoConnection = ConfigurationGuard.Require(
            connectionStrings.MongoDb,
            $"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.MongoDb)}");
        var mongoDatabase = configuration["MongoDB:Database"] ?? "payment-gateway";

        var postgresConnection = ConfigurationGuard.Require(
            connectionStrings.DefaultConnection,
            $"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.DefaultConnection)}");

        var configuredRedisConnection = string.IsNullOrWhiteSpace(redisOptions.ConnectionString)
            ? connectionStrings.Redis
            : redisOptions.ConnectionString;
        var redisConnection = ConfigurationGuard.Require(
            configuredRedisConnection,
            $"{RedisOptions.SectionName}:{nameof(RedisOptions.ConnectionString)}");

        return new InfrastructureRegistration(
            mongoConnection,
            mongoDatabase,
            postgresConnection,
            redisConnection,
            redisOptions.InstanceName,
            ddsOptions.UseRealDds,
            // PRODUCTION: DDS configuration with circuit breaker
            DdsTelemetrySamplingRate: ddsOptions.TelemetrySamplingRate,
            CircuitBreakerFailureThreshold: ddsOptions.BatchSize > 0 ? 5 : 3, // Conservative on high-load
            CircuitBreakerRecoveryIntervalSeconds: 30);
    }

    public static IServiceCollection AddPaymentGatewayMongoPersistence(
        this IServiceCollection services,
        InfrastructureRegistration registration,
        ServiceLifetime repositoryLifetime,
        bool addPaymentRepository = false,
        bool addWebhookRepository = false,
        bool addTransactionEventRepository = false)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMongoDbContext>(_ => new MongoDbContext(registration.MongoConnection, registration.MongoDatabase));

        if (addPaymentRepository)
        {
            services.AddWithLifetime<IPaymentRepository, PaymentRepository>(repositoryLifetime);
        }

        if (addWebhookRepository)
        {
            services.AddWithLifetime<IWebhookRepository, WebhookRepository>(repositoryLifetime);
        }

        if (addTransactionEventRepository)
        {
            services.AddWithLifetime<ITransactionEventRepository, TransactionEventRepository>(repositoryLifetime);
        }

        return services;
    }

    public static IServiceCollection AddPaymentGatewayRedisCaching(
        this IServiceCollection services,
        string redisConnection,
        string redisInstanceName,
        ServiceLifetime cacheLifetime)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(redisConnection);

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = redisInstanceName;
        });
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
        services.AddWithLifetime<IRedisService, RedisService>(cacheLifetime);

        return services;
    }

    public static IServiceCollection AddPaymentGatewayDds(
        this IServiceCollection services,
        bool useRealDds,
        bool addSubscriber,
        bool registerPublisherAbstraction = false,
        bool registerFallbackSubscriber = false)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (useRealDds)
        {
            services.AddSingleton(sp =>
                new CycloneDdsPublisher(
                    sp.GetService<ILogger<CycloneDdsPublisher>>(),
                    useRealDds: true));

            if (addSubscriber)
            {
                services.AddSingleton(sp =>
                    new CycloneDdsSubscriber(
                        sp.GetService<ILogger<CycloneDdsSubscriber>>(),
                        useRealDds: true));
            }
        }
        else
        {
            services.AddSingleton<InMemoryDdsPublisher>();

            if (addSubscriber && registerFallbackSubscriber)
            {
                services.AddSingleton(sp =>
                    new CycloneDdsSubscriber(
                        sp.GetService<ILogger<CycloneDdsSubscriber>>(),
                        useRealDds: false));
            }
        }

        if (registerPublisherAbstraction)
        {
            services.AddSingleton<IDdsPublisher>(sp =>
                useRealDds
                    ? sp.GetRequiredService<CycloneDdsPublisher>()
                    : sp.GetRequiredService<InMemoryDdsPublisher>());
        }

        return services;
    }

    public static IServiceCollection AddPaymentGatewayHttpClient(
        this IServiceCollection services,
        string name,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        services.AddHttpClient(name, client => client.Timeout = timeout);
        return services;
    }

    private static IServiceCollection AddWithLifetime<TService, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime)
        where TService : class
        where TImplementation : class, TService
    {
        return lifetime switch
        {
            ServiceLifetime.Singleton => services.AddSingleton<TService, TImplementation>(),
            ServiceLifetime.Scoped => services.AddScoped<TService, TImplementation>(),
            ServiceLifetime.Transient => services.AddTransient<TService, TImplementation>(),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unsupported service lifetime.")
        };
    }
}
