using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaymentGateway.Infrastructure.Health;
using PaymentGateway.Infrastructure.DDS;
using Moq;

namespace PaymentGateway.Benchmarks;

/// <summary>
/// Benchmarks health check and recovery monitoring overhead from Phase 3.
/// Measures: health check latency, recovery monitor CPU/memory impact.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[MedianColumn, Min95Column, Max95Column, StdDevColumn, AllocationColumn]
[WarmupCount(3), TargetCount(10)]
public class HealthCheckBenchmark
{
    private DdsHealthCheck _healthCheck = null!;
    private ILogger<DdsHealthCheck> _logger = null!;
    private Mock<IDdsPublisher> _mockPublisher = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(x => x.AddConsole());
        var sp = services.BuildServiceProvider();
        _logger = sp.GetRequiredService<ILogger<DdsHealthCheck>>();

        _mockPublisher = new Mock<IDdsPublisher>();
        _mockPublisher
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _healthCheck = new DdsHealthCheck(_mockPublisher.Object, _logger);
    }

    /// <summary>
    /// Measure: Health check execution latency (successful DDS).
    /// This runs on the health check endpoint and should be < 5 seconds.
    /// </summary>
    [Benchmark(Description = "Health check (healthy)")]
    public async Task HealthCheckHealthy()
    {
        var context = new HealthCheckContext();
        await _healthCheck.CheckHealthAsync(context);
    }

    /// <summary>
    /// Measure: Health check execution with DDS unavailable.
    /// Should timeout gracefully after 5 seconds.
    /// </summary>
    [Benchmark(Description = "Health check (timeout)")]
    [IterationSetup(Target = nameof(HealthCheckTimeout))]
    public void SetupTimeout()
    {
        _mockPublisher.Reset();
        _mockPublisher
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                // Simulate slow DDS (will be interrupted by timeout)
                await Task.Delay(10000);
            });

        _healthCheck = new DdsHealthCheck(_mockPublisher.Object, _logger);
    }

    public async Task HealthCheckTimeout()
    {
        var context = new HealthCheckContext();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await _healthCheck.CheckHealthAsync(context, cts.Token);
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Measure: Repeated health checks (simulating polling every 5 seconds).
    /// </summary>
    [Benchmark(Description = "Health check (10 iterations)")]
    public async Task HealthCheckRepeated()
    {
        var context = new HealthCheckContext();
        for (int i = 0; i < 10; i++)
        {
            await _healthCheck.CheckHealthAsync(context);
        }
    }

    /// <summary>
    /// Measure: Health check data extraction overhead.
    /// The health check returns detailed diagnostic data.
    /// </summary>
    [Benchmark(Description = "Build health check response")]
    public HealthCheckResult BuildHealthResponse()
    {
        return new HealthCheckResult(
            status: HealthStatus.Healthy,
            description: "DDS is available",
            data: new Dictionary<string, object>
            {
                { "dds_available", true },
                { "check_interval_ms", 5000 },
                { "timestamp_utc", DateTime.UtcNow },
                { "status", "healthy" }
            });
    }

    /// <summary>
    /// Measure: Recovery monitor state check overhead.
    /// Called every 5 seconds to check if circuit should recover.
    /// </summary>
    [Benchmark(Description = "Recovery monitor state check")]
    public void RecoveryMonitorCheck()
    {
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 5,
            RecoveryIntervalSeconds = 30,
            Enabled = true
        };

        var cb = new CircuitBreakerPublisher(_mockPublisher.Object, _logger, options);
        cb.CheckRecovery();
    }

    /// <summary>
    /// Measure: Recovery monitor loop iteration cost.
    /// Simulates what happens every 5 seconds in DdsRecoveryMonitor.
    /// </summary>
    [Benchmark(Description = "Recovery monitor iteration")]
    public async Task RecoveryMonitorIteration()
    {
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 5,
            RecoveryIntervalSeconds = 30,
            Enabled = true
        };

        var cb = new CircuitBreakerPublisher(_mockPublisher.Object, _logger, options);

        // Simulate one iteration of the monitor loop
        cb.CheckRecovery();
        var (state, failureCount, threshold) = cb.GetStatus();

        // Simulate logging the state
        _ = state.ToString();
    }

    /// <summary>
    /// Measure: Multiple health checks in parallel (simulating concurrent requests).
    /// </summary>
    [Benchmark(Description = "Concurrent health checks (5 parallel)")]
    public async Task ConcurrentHealthChecks()
    {
        var context = new HealthCheckContext();
        var tasks = new Task[5];

        for (int i = 0; i < 5; i++)
        {
            tasks[i] = _healthCheck.CheckHealthAsync(context);
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Measure: Health check endpoint response serialization.
    /// </summary>
    [Benchmark(Description = "Health check JSON serialization")]
    public string HealthCheckResponseSerialization()
    {
        var response = new
        {
            status = "Healthy",
            checks = new
            {
                dds = new
                {
                    status = "Healthy",
                    data = new
                    {
                        dds_available = true,
                        check_interval_ms = 5000,
                        timestamp_utc = DateTime.UtcNow,
                        status = "healthy"
                    }
                }
            }
        };

        return System.Text.Json.JsonSerializer.Serialize(response);
    }
}
