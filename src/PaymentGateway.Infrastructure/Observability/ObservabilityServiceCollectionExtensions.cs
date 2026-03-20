using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Infrastructure.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;

namespace PaymentGateway.Infrastructure.Observability;

public static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentGatewayObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool addAspNetCoreInstrumentation = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var telemetryOptions = configuration
            .GetSection(TelemetryOptions.SectionName)
            .Get<TelemetryOptions>() ?? new TelemetryOptions();

        services.Configure<TelemetryOptions>(configuration.GetSection(TelemetryOptions.SectionName));

        var logger = BuildLogger(configuration, telemetryOptions, serviceName);

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(logger, dispose: true);
        });

        var environmentName = ResolveEnvironmentName(telemetryOptions);
        var serviceVersion = ResolveServiceVersion(telemetryOptions);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = environmentName
                }))
            .WithTracing(tracing =>
            {
                tracing.AddSource(PaymentGatewayTelemetry.ActivitySourceName);
                tracing.AddHttpClientInstrumentation();

                if (addAspNetCoreInstrumentation)
                {
                    tracing.AddAspNetCoreInstrumentation();
                }

                ConfigureTraceExporters(tracing, telemetryOptions);
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(PaymentGatewayTelemetry.MeterName);
                metrics.AddRuntimeInstrumentation();

                if (addAspNetCoreInstrumentation)
                {
                    metrics.AddAspNetCoreInstrumentation();
                }

                ConfigureMetricExporters(metrics, telemetryOptions);
            });

        return services;
    }

    private static void ConfigureTraceExporters(TracerProviderBuilder tracing, TelemetryOptions options)
    {
        if (options.EnableOtlp && Uri.TryCreate(options.OtelEndpoint, UriKind.Absolute, out var endpoint))
        {
            tracing.AddOtlpExporter(exporter =>
            {
                exporter.Endpoint = endpoint;
                exporter.Protocol = OtlpExportProtocol.Grpc;
            });
        }

        if (options.EnableConsoleExporter)
        {
            tracing.AddConsoleExporter();
        }
    }

    private static void ConfigureMetricExporters(MeterProviderBuilder metrics, TelemetryOptions options)
    {
        if (options.EnableOtlp && Uri.TryCreate(options.OtelEndpoint, UriKind.Absolute, out var endpoint))
        {
            metrics.AddOtlpExporter(exporter =>
            {
                exporter.Endpoint = endpoint;
                exporter.Protocol = OtlpExportProtocol.Grpc;
            });
        }

        if (options.EnableConsoleExporter)
        {
            metrics.AddConsoleExporter();
        }
    }

    private static Serilog.ILogger BuildLogger(
        IConfiguration configuration,
        TelemetryOptions options,
        string serviceName)
    {
        var environmentName = ResolveEnvironmentName(options);
        var serviceVersion = ResolveServiceVersion(options);

        var loggerConfiguration = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With(new ActivityEnricher())
            .Enrich.WithProperty("ServiceName", serviceName)
            .Enrich.WithProperty("Environment", environmentName)
            .Enrich.WithProperty("ServiceVersion", serviceVersion)
            .WriteTo.Console();

        if (options.EnableElasticsearchLogging &&
            Uri.TryCreate(options.ElasticsearchUrl, UriKind.Absolute, out var elasticsearchUri))
        {
            loggerConfiguration.WriteTo.Elasticsearch(CreateElasticsearchSinkOptions(
                elasticsearchUri,
                options,
                serviceName,
                environmentName));
        }

        return loggerConfiguration.CreateLogger();
    }

    private static ElasticsearchSinkOptions CreateElasticsearchSinkOptions(
        Uri elasticsearchUri,
        TelemetryOptions options,
        string serviceName,
        string environmentName)
    {
        return new ElasticsearchSinkOptions(elasticsearchUri)
        {
            AutoRegisterTemplate = true,
            IndexFormat = $"{NormalizeIndexName(serviceName)}-{environmentName.ToLowerInvariant()}-{DateTime.UtcNow:yyyy.MM}",
            ModifyConnectionSettings = connection =>
            {
                if (!string.IsNullOrWhiteSpace(options.ElasticsearchUsername) &&
                    !string.IsNullOrWhiteSpace(options.ElasticsearchPassword))
                {
                    connection = connection.BasicAuthentication(
                        options.ElasticsearchUsername,
                        options.ElasticsearchPassword);
                }

                if (options.SkipTlsValidation)
                {
                    connection = connection.ServerCertificateValidationCallback((_, _, _, _) => true);
                }

                return connection;
            },
            EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog
        };
    }

    private static string ResolveEnvironmentName(TelemetryOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.DeploymentEnvironment))
            return options.DeploymentEnvironment;

        return Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";
    }

    private static string ResolveServiceVersion(TelemetryOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ServiceVersion))
            return options.ServiceVersion;

        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
    }

    private static string NormalizeIndexName(string serviceName)
    {
        return serviceName
            .ToLowerInvariant()
            .Replace('.', '-')
            .Replace('_', '-');
    }
}
