namespace PaymentGateway.Infrastructure.DDS;

/// <summary>
/// Configuration for DDS services.
/// </summary>
public class DdsConfiguration
{
    /// <summary>
    /// Use real CycloneDDS (production) vs in-memory fallback (development).
    /// </summary>
    public bool UseRealDds { get; set; } = true;

    /// <summary>
    /// Telemetry sampling rate (0.0-1.0).
    /// 0.1 = sample 10% of messages for reduced overhead.
    /// </summary>
    public float TelemetrySamplingRate { get; set; } = 0.1f;

    /// <summary>
    /// Enable circuit breaker for graceful degradation on DDS failure.
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    /// Number of consecutive failures before opening circuit breaker.
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Seconds to wait before attempting DDS recovery (transitioning to Half-Open).
    /// </summary>
    public int CircuitBreakerRecoveryIntervalSeconds { get; set; } = 30;
}
