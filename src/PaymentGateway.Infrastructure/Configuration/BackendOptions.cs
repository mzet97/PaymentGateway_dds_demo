namespace PaymentGateway.Infrastructure.Configuration;

public sealed class ConnectionStringsOptions
{
    public const string SectionName = "ConnectionStrings";

    public string DefaultConnection { get; set; } = "__CONNECTIONSTRINGS__DEFAULTCONNECTION__";

    public string MongoDb { get; set; } = "__CONNECTIONSTRINGS__MONGODB__";

    public string Redis { get; set; } = "__CONNECTIONSTRINGS__REDIS__";
}

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "__REDIS__CONNECTIONSTRING__";

    public string InstanceName { get; set; } = "demo-gateway_";

    public string SessionKeyPrefix { get; set; } = "sess:";

    public string ConnectionKeyPrefix { get; set; } = "conn:";

    public string ParticipantKeyPrefix { get; set; } = "part:";

    public string SignalRKeyPrefix { get; set; } = "signalr:";
}

public sealed class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    public string Model { get; set; } = "minimax/minimax-m2.5";
}

public sealed class MinioOptions
{
    public const string SectionName = "Minio";

    public string Endpoint { get; set; } = "minio-s3.home.arpa";

    public string AccessKey { get; set; } = "__MINIO__ACCESSKEY__";

    public string SecretKey { get; set; } = "__MINIO__SECRETKEY__";

    public string BucketName { get; set; } = "demo-gateway";

    public bool UseSsl { get; set; } = true;
}

public sealed class DdsOptions
{
    public const string SectionName = "Dds";

    /// <summary>
    /// Enable real CycloneDDS (production) vs in-memory fallback mode.
    /// </summary>
    public bool UseRealDds { get; set; } = true;

    /// <summary>
    /// Path to native DDS library directory.
    /// On Linux: libddsc.so location (e.g., /usr/local/lib or ./artifacts/native/linux-x64)
    /// On Windows: ddsc.dll location
    /// If empty, uses system library paths (LD_LIBRARY_PATH, PATH).
    /// </summary>
    public string NativeLibraryPath { get; set; } = string.Empty;

    /// <summary>
    /// DDS initialization timeout in milliseconds (default 5000ms).
    /// Prevents hanging on slow or unavailable DDS infrastructure.
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Enable graceful fallback to in-memory mode if DDS initialization fails.
    /// In production, set to false to fail-fast on infrastructure issues.
    /// </summary>
    public bool EnableGracefulFallback { get; set; } = false;

    /// <summary>
    /// Telemetry sampling rate (0.0 to 1.0).
    /// 0.1 = sample 10% of messages for reduced overhead.
    /// 1.0 = trace all messages.
    /// 0.0 = disable telemetry entirely.
    /// </summary>
    public float TelemetrySamplingRate { get; set; } = 0.1f;

    /// <summary>
    /// Maximum number of messages to batch before publishing (0 = no batching).
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum time in milliseconds to wait before flushing batched messages.
    /// </summary>
    public int BatchTimeoutMs { get; set; } = 10;
}

public sealed class AuthentikOptions
{
    public const string SectionName = "Authentik";

    public string Authority { get; set; } = "https://authentik.home.arpa";

    public string Audience { get; set; } = "payment-gateway";

    public string? ValidIssuer { get; set; }

    public string[] ValidAudiences { get; set; } = Array.Empty<string>();

    public bool RequireHttpsMetadata { get; set; } = true;

    public string MerchantIdClaim { get; set; } = "merchant_id";

    public string[] RoleClaims { get; set; } = new[] { "roles", "role", "groups" };

    public string[] AdminRoles { get; set; } = new[] { "Admin", "admin" };
}
