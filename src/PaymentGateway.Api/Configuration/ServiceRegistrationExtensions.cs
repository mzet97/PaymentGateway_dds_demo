using PaymentGateway.Api.Common.Authorization;
using PaymentGateway.Api.Common.Realtime;
using PaymentGateway.Api.Middlewares;
using PaymentGateway.Application.Services;
using PaymentGateway.Application.Configuration;
using PaymentGateway.Infrastructure.Configuration;
using PaymentGateway.Infrastructure.DDS;
using PaymentGateway.Infrastructure.Health;
using PaymentGateway.Infrastructure.Observability;

namespace PaymentGateway.Api.Configuration;

public static class ServiceRegistrationExtensions
{
    public static WebApplicationBuilder AddPaymentGatewayServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddPaymentGatewayObservability(
            builder.Configuration,
            "PaymentGateway.Api",
            addAspNetCoreInstrumentation: true);

        builder.Services.AddScoped<IMerchantScopeService, MerchantScopeService>();
        builder.Services.AddPaymentGatewayApplicationServices();
        builder.Services.AddGatewayAuthentication(builder.Configuration);
        var infrastructure = builder.Services.AddPaymentGatewayInfrastructure(builder.Configuration);
        builder.Services.AddSingleton<IPaymentUpdatesNotifier, PaymentUpdatesNotifier>();

        // PRODUCTION: Configure DDS with circuit breaker and health checks
        builder.Services.AddSingleton<IDdsPublisher>(sp =>
        {
            var ddsLogger = sp.GetRequiredService<ILogger<CycloneDdsPublisher>>();

            // Create base publisher (CycloneDDS or in-memory)
            IDdsPublisher basePublisher = infrastructure.UseRealDds
                ? (IDdsPublisher)new CycloneDdsPublisher(
                    ddsLogger,
                    useRealDds: true,
                    telemetrySamplingRate: infrastructure.DdsTelemetrySamplingRate)
                : new InMemoryDdsPublisher();

            // Wrap with circuit breaker for graceful fallback
            var circuitBreakerLogger = sp.GetRequiredService<ILogger<CircuitBreakerPublisher>>();
            var circuitBreaker = new CircuitBreakerPublisher(
                basePublisher,
                circuitBreakerLogger,
                new CircuitBreakerOptions
                {
                    FailureThreshold = infrastructure.CircuitBreakerFailureThreshold,
                    RecoveryIntervalSeconds = infrastructure.CircuitBreakerRecoveryIntervalSeconds,
                    Enabled = infrastructure.UseRealDds
                });

            // Wrap with broadcasting for real-time updates
            return new BroadcastingDdsPublisher(
                circuitBreaker,
                sp.GetRequiredService<IPaymentUpdatesNotifier>(),
                sp.GetRequiredService<ILogger<BroadcastingDdsPublisher>>());
        });

        // Register circuit breaker as hosted service for recovery monitoring
        builder.Services.AddHostedService(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DdsRecoveryMonitor>>();
            var publisher = sp.GetRequiredService<IDdsPublisher>();
            return new DdsRecoveryMonitor(logger, publisher);
        });

        // Register health checks
        var healthChecks = builder.Services.AddHealthChecks()
            .AddMongoDb(infrastructure.MongoConnection, name: "mongodb")
            .AddNpgSql(infrastructure.PostgresConnection, name: "postgresql")
            .AddRedis(infrastructure.RedisConnection, name: "redis");

        // Add DDS health check
        builder.Services.AddSingleton<DdsHealthCheck>();
        healthChecks.AddCheck<DdsHealthCheck>(
            "dds",
            tags: new[] { "dds", "infrastructure" });

        return builder;
    }
}
