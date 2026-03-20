namespace PaymentGateway.Infrastructure.DDS;

/// <summary>
/// Configuration options for circuit breaker.
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Number of consecutive failures before opening circuit.
    /// Default: 5 failures
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Seconds to wait before attempting recovery (Half-Open state).
    /// Default: 30 seconds
    /// </summary>
    public int RecoveryIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Enable circuit breaker (default true).
    /// Set to false to disable and always use real DDS (fail-fast).
    /// </summary>
    public bool Enabled { get; set; } = true;
}
