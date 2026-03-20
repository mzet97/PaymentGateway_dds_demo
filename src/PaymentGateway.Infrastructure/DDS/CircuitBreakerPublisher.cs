using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PaymentGateway.Application.Services;

namespace PaymentGateway.Infrastructure.DDS;

/// <summary>
/// Circuit breaker pattern for DDS publisher.
/// Provides graceful degradation when DDS is unavailable.
///
/// States:
/// - Closed: DDS healthy, publish normally
/// - Open: DDS failed, fall back to in-memory mode
/// - Half-Open: Testing if DDS recovered, retry limited messages
///
/// Behavior:
/// - Counts consecutive failures
/// - Opens circuit after threshold (default 5 failures)
/// - Retries recovery every interval (default 30 seconds)
/// - Logs all state transitions for observability
/// </summary>
public sealed class CircuitBreakerPublisher : IDdsPublisher, IDisposable
{
    private readonly IDdsPublisher _innerPublisher;
    private readonly ILogger<CircuitBreakerPublisher> _logger;
    private readonly CircuitBreakerOptions _options;

    // Circuit breaker state
    private volatile CircuitState _state = CircuitState.Closed;
    private int _failureCount = 0;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private DateTime _openedAt = DateTime.MinValue;
    private bool _disposed = false;

    /// <summary>
    /// Circuit breaker states.
    /// </summary>
    private enum CircuitState
    {
        /// <summary>Normal operation - DDS available</summary>
        Closed,

        /// <summary>DDS unavailable - using fallback</summary>
        Open,

        /// <summary>Testing DDS recovery</summary>
        HalfOpen
    }

    public CircuitBreakerPublisher(
        IDdsPublisher innerPublisher,
        ILogger<CircuitBreakerPublisher> logger,
        CircuitBreakerOptions? options = null)
    {
        _innerPublisher = innerPublisher ?? throw new ArgumentNullException(nameof(innerPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new CircuitBreakerOptions();

        _logger.LogInformation(
            "Circuit breaker initialized (failure_threshold: {FailureThreshold}, recovery_interval: {RecoveryIntervalSeconds}s)",
            _options.FailureThreshold,
            _options.RecoveryIntervalSeconds);
    }

    public async Task PublishAsync<T>(string topic, T data, CancellationToken ct = default) where T : class
    {
        if (_state == CircuitState.Closed)
        {
            // CLOSED: Try to publish normally
            try
            {
                await _innerPublisher.PublishAsync(topic, data, ct);
                ResetFailureCount(); // Success - reset counter
                return;
            }
            catch (Exception ex)
            {
                RecordFailure(ex, topic);
                // Fall through to fallback
            }
        }

        if (_state == CircuitState.HalfOpen)
        {
            // HALF-OPEN: Limited retry to test recovery
            try
            {
                await _innerPublisher.PublishAsync(topic, data, ct);
                TransitionToClosed(); // Recovered!
                return;
            }
            catch (Exception ex)
            {
                RecordFailure(ex, topic);
                TransitionToOpen(); // Still failing
                // Fall through to fallback
            }
        }

        // OPEN or recovered failure: Use fallback mode
        PublishToFallback(topic, data);
    }

    /// <summary>
    /// Publish to in-memory fallback mode.
    /// Messages are logged but not sent to DDS.
    /// </summary>
    private void PublishToFallback<T>(string topic, T data) where T : class
    {
        var timestamp = DateTimeOffset.UtcNow;
        var circuitStatus = _state.ToString();

        _logger.LogWarning(
            "[FALLBACK] Publishing to {Topic} (circuit: {CircuitState}, uptime: {UptimeSecs}s) | {@Payload}",
            topic,
            circuitStatus,
            _state == CircuitState.Open ? (long)(timestamp - _openedAt).TotalSeconds : 0,
            data);

        Console.WriteLine(
            $"[DDS FALLBACK] Topic: {topic}, Circuit: {circuitStatus}, Payload: {System.Text.Json.JsonSerializer.Serialize(data)}");
    }

    /// <summary>
    /// Record a publish failure and check if circuit should open.
    /// </summary>
    private void RecordFailure(Exception ex, string topic)
    {
        _lastFailureTime = DateTime.UtcNow;
        _failureCount++;

        if (_failureCount >= _options.FailureThreshold)
        {
            TransitionToOpen();
        }
        else
        {
            _logger.LogWarning(
                ex,
                "DDS publish failed ({FailureCount}/{FailureThreshold}) on topic {Topic}",
                _failureCount,
                _options.FailureThreshold,
                topic);
        }
    }

    /// <summary>
    /// Reset failure counter after successful publish.
    /// </summary>
    private void ResetFailureCount()
    {
        if (_failureCount > 0)
        {
            _logger.LogDebug("DDS failure counter reset (was {FailureCount})", _failureCount);
            _failureCount = 0;
        }
    }

    /// <summary>
    /// Transition to OPEN state (DDS unavailable).
    /// </summary>
    private void TransitionToOpen()
    {
        if (_state == CircuitState.Open)
            return; // Already open

        _state = CircuitState.Open;
        _openedAt = DateTime.UtcNow;
        _logger.LogError(
            "Circuit breaker OPENED - DDS unavailable. " +
            "Falling back to in-memory mode. Recovery in {RecoveryIntervalSeconds}s.",
            _options.RecoveryIntervalSeconds);
    }

    /// <summary>
    /// Transition to HALF-OPEN state (testing recovery).
    /// </summary>
    private void TransitionToHalfOpen()
    {
        _state = CircuitState.HalfOpen;
        _failureCount = 0;
        _logger.LogWarning(
            "Circuit breaker HALF-OPEN - testing DDS recovery");
    }

    /// <summary>
    /// Transition to CLOSED state (DDS recovered).
    /// </summary>
    private void TransitionToClosed()
    {
        if (_state == CircuitState.Closed)
            return; // Already closed

        var downtime = (DateTime.UtcNow - _openedAt).TotalSeconds;
        _state = CircuitState.Closed;
        _failureCount = 0;
        _logger.LogInformation(
            "Circuit breaker CLOSED - DDS recovered after {DowntimeSeconds:F1}s downtime",
            downtime);
    }

    /// <summary>
    /// Check if recovery window has passed and try half-open.
    /// Called periodically (e.g., from background task or health check).
    /// </summary>
    public void CheckRecovery()
    {
        if (_state != CircuitState.Open)
            return;

        var timeSinceOpen = DateTime.UtcNow - _openedAt;
        if (timeSinceOpen.TotalSeconds >= _options.RecoveryIntervalSeconds)
        {
            TransitionToHalfOpen();
            _logger.LogInformation(
                "Recovery interval reached ({IntervalSeconds}s) - entering half-open state",
                _options.RecoveryIntervalSeconds);
        }
    }

    /// <summary>
    /// Get current circuit breaker state for monitoring.
    /// </summary>
    public (CircuitBreakerState State, int FailureCount, int FailureThreshold) GetStatus()
    {
        return (_state switch
        {
            CircuitState.Closed => CircuitBreakerState.Closed,
            CircuitState.Open => CircuitBreakerState.Open,
            CircuitState.HalfOpen => CircuitBreakerState.HalfOpen,
            _ => CircuitBreakerState.Unknown
        }, _failureCount, _options.FailureThreshold);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_innerPublisher is IDisposable disposable)
            disposable.Dispose();

        _logger.LogInformation(
            "Circuit breaker disposed (final_state: {State}, failure_count: {FailureCount})",
            _state,
            _failureCount);
    }
}

