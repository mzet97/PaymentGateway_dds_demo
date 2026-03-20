using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentGateway.Application.Services;

namespace PaymentGateway.Infrastructure.DDS;

/// <summary>
/// Background service that monitors DDS circuit breaker state.
/// Periodically checks if DDS has recovered from failures.
/// Transitions circuit breaker from Open → Half-Open when recovery window passes.
/// </summary>
public sealed class DdsRecoveryMonitor : BackgroundService
{
    private readonly CircuitBreakerPublisher? _circuitBreaker;
    private readonly ILogger<DdsRecoveryMonitor> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);

    public DdsRecoveryMonitor(
        ILogger<DdsRecoveryMonitor> logger,
        IDdsPublisher? publisher = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // OPTIONAL: If publisher is wrapped in circuit breaker, monitor it
        _circuitBreaker = publisher as CircuitBreakerPublisher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_circuitBreaker == null)
        {
            _logger.LogInformation("No circuit breaker to monitor - using standard publisher");
            return;
        }

        _logger.LogInformation(
            "DDS recovery monitor started (check interval: {IntervalSeconds}s)",
            _checkInterval.TotalSeconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Check if circuit breaker should attempt recovery
                    _circuitBreaker.CheckRecovery();

                    // Get current status for logging
                    var (state, failureCount, threshold) = _circuitBreaker.GetStatus();

                    if (state != CircuitBreakerState.Closed)
                    {
                        _logger.LogWarning(
                            "DDS circuit breaker state: {State} (failures: {FailureCount}/{Threshold})",
                            state,
                            failureCount,
                            threshold);
                    }

                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in DDS recovery monitor");
                    // Continue monitoring despite errors
                    try
                    {
                        await Task.Delay(_checkInterval, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            _logger.LogInformation("DDS recovery monitor stopped");
        }
    }
}
