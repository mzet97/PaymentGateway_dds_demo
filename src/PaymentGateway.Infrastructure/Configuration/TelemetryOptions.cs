namespace PaymentGateway.Infrastructure.Configuration;

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public bool EnableOtlp { get; set; }
    public string? OtelEndpoint { get; set; }
    public bool EnableConsoleExporter { get; set; }

    public bool EnableElasticsearchLogging { get; set; }
    public string? ElasticsearchUrl { get; set; }
    public string? ElasticsearchUsername { get; set; }
    public string? ElasticsearchPassword { get; set; }
    public bool SkipTlsValidation { get; set; }

    public string? DeploymentEnvironment { get; set; }
    public string? ServiceVersion { get; set; }
}
