using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using PaymentGateway.Application.Services;
using PaymentGateway.Infrastructure.Health;

namespace PaymentGateway.Infrastructure.DDS;

/// <summary>
/// Extension methods for registering DDS services with dependency injection.
/// Handles registration of:
/// - DDS publisher with circuit breaker
/// - DDS subscriber
/// - Health checks
/// - Recovery monitoring background service
/// </summary>
public static class DdsServiceCollectionExtensions
{
    /// <summary>
    /// Register DDS publisher and subscriber with production-ready resilience patterns.
    /// Configures:
    /// - Circuit breaker for graceful fallback
    /// - Health checks
    /// - Recovery monitoring
    /// </summary>
    public static IServiceCollection AddDdsServices(
        this IServiceCollection services,
        DdsConfiguration config)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        // Register base publisher (will be wrapped with circuit breaker)
        services.AddSingleton<IDdsPublisher>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<CycloneDdsPublisher>>();
            return new CycloneDdsPublisher(
                logger,
                useRealDds: config.UseRealDds,
                telemetrySamplingRate: config.TelemetrySamplingRate);
        });

        // Wrap with circuit breaker if enabled
        if (config.EnableCircuitBreaker)
        {
            services.Decorate<IDdsPublisher>((publisher, sp) =>
            {
                var logger = sp.GetRequiredService<ILogger<CircuitBreakerPublisher>>();
                var options = new CircuitBreakerOptions
                {
                    FailureThreshold = config.CircuitBreakerFailureThreshold,
                    RecoveryIntervalSeconds = config.CircuitBreakerRecoveryIntervalSeconds,
                    Enabled = config.EnableCircuitBreaker
                };

                return new CircuitBreakerPublisher(publisher, logger, options);
            });
        }

        // Register subscriber
        services.AddSingleton<CycloneDdsSubscriber>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<CycloneDdsSubscriber>>();
            return new CycloneDdsSubscriber(
                logger,
                useRealDds: config.UseRealDds,
                telemetrySamplingRate: config.TelemetrySamplingRate);
        });

        // Register health check
        services.AddSingleton<DdsHealthCheck>();
        services.AddHealthChecks()
            .AddCheck<DdsHealthCheck>(
                "dds",
                HealthStatus.Unhealthy,
                tags: new[] { "dds", "infrastructure" });

        // Register recovery monitor background service (only if circuit breaker enabled)
        if (config.EnableCircuitBreaker)
        {
            services.AddHostedService(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DdsRecoveryMonitor>>();
                var publisher = sp.GetRequiredService<IDdsPublisher>();
                return new DdsRecoveryMonitor(logger, publisher);
            });
        }

        return services;
    }

    /// <summary>
    /// Decorator pattern for decorating registered services.
    /// Allows wrapping a service with additional functionality.
    /// </summary>
    private static IServiceCollection Decorate<TInterface>(
        this IServiceCollection services,
        Func<TInterface, IServiceProvider, TInterface> decorate)
        where TInterface : class
    {
        var wrappedDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(TInterface));
        if (wrappedDescriptor == null)
            throw new InvalidOperationException($"{typeof(TInterface).Name} is not registered");

        var newDescriptor = ServiceDescriptor.Describe(
            typeof(TInterface),
            provider =>
            {
                var wrappedInstance = (TInterface)provider.CreateInstance(wrappedDescriptor)!;
                return decorate(wrappedInstance, provider);
            },
            wrappedDescriptor.Lifetime);

        // Remove all existing descriptors for this interface and add the decorated one
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(TInterface))
            {
                services.RemoveAt(i);
            }
        }
        services.Add(newDescriptor);

        return services;
    }

    private static object? CreateInstance(this IServiceProvider provider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance != null)
            return descriptor.ImplementationInstance;

        if (descriptor.ImplementationFactory != null)
            return descriptor.ImplementationFactory(provider);

        return ActivatorUtilities.GetServiceOrCreateInstance(provider, descriptor.ImplementationType!);
    }
}

