namespace PaymentGateway.Infrastructure.DDS;

/// <summary>
/// Public circuit breaker state for monitoring.
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>DDS available - normal operation</summary>
    Closed = 0,

    /// <summary>DDS unavailable - fallback mode</summary>
    Open = 1,

    /// <summary>Testing DDS recovery</summary>
    HalfOpen = 2,

    /// <summary>Unknown state</summary>
    Unknown = -1
}
