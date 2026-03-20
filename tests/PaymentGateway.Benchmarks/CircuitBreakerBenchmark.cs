using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaymentGateway.Infrastructure.DDS;
using Moq;

namespace PaymentGateway.Benchmarks;

/// <summary>
/// Benchmarks circuit breaker overhead from Phase 3.
/// Measures: normal operation latency, open/half-open state overhead.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[MedianColumn, Min95Column, Max95Column, StdDevColumn, AllocationColumn]
[WarmupCount(3), TargetCount(10)]
public class CircuitBreakerBenchmark
{
    private CircuitBreakerPublisher _circuitBreaker = null!;
    private Mock<IDdsPublisher> _innerPublisher = null!;
    private ILogger<CircuitBreakerPublisher> _logger = null!;
    private CircuitBreakerOptions _options = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(x => x.AddConsole());
        var sp = services.BuildServiceProvider();
        _logger = sp.GetRequiredService<ILogger<CircuitBreakerPublisher>>();

        _options = new CircuitBreakerOptions
        {
            FailureThreshold = 5,
            RecoveryIntervalSeconds = 30,
            Enabled = true
        };

        _innerPublisher = new Mock<IDdsPublisher>();
        _innerPublisher
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _circuitBreaker = new CircuitBreakerPublisher(
            _innerPublisher.Object,
            _logger,
            _options);
    }

    /// <summary>
    /// Baseline: Direct publisher call (no circuit breaker).
    /// </summary>
    [Benchmark(Description = "Direct publisher (baseline)")]
    public async Task DirectPublish()
    {
        await _innerPublisher.Object.PublishAsync("payment.create", new { Amount = 100.0 }, CancellationToken.None);
    }

    /// <summary>
    /// Measure: Circuit breaker overhead when CLOSED (normal operation).
    /// Should add minimal latency (~0-0.5ms).
    /// </summary>
    [Benchmark(Description = "Circuit breaker (CLOSED)")]
    public async Task CircuitBreakerClosed()
    {
        await _circuitBreaker.PublishAsync("payment.create", new { Amount = 100.0 }, CancellationToken.None);
    }

    /// <summary>
    /// Measure: Get circuit breaker status (state check).
    /// Called frequently by recovery monitor.
    /// </summary>
    [Benchmark(Description = "Check circuit state")]
    public (CircuitBreakerState State, int FailureCount, int Threshold) CheckState()
    {
        return _circuitBreaker.GetStatus();
    }

    /// <summary>
    /// Measure: Circuit breaker state transition latency.
    /// Tests Open -> Half-Open transition overhead.
    /// </summary>
    [Benchmark(Description = "State transition check")]
    public void CheckRecovery()
    {
        _circuitBreaker.CheckRecovery();
    }

    /// <summary>
    /// Scenario: Simulate 5 failures then measure recovery attempt.
    /// </summary>
    [IterationSetup(Target = nameof(CircuitBreakerOpenState))]
    public void SetupOpen()
    {
        _circuitBreaker = new CircuitBreakerPublisher(
            _innerPublisher.Object,
            _logger,
            _options);

        _innerPublisher.Reset();
        _innerPublisher
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("DDS unavailable"));

        // Trigger 5 failures to open circuit
        for (int i = 0; i < 5; i++)
        {
            try
            {
                _circuitBreaker.PublishAsync("payment.create", new { Amount = 100.0 }, CancellationToken.None).Wait();
            }
            catch { /* Expected */ }
        }
    }

    /// <summary>
    /// Measure: Circuit breaker overhead when OPEN (fast-fail).
    /// Should add minimal latency due to in-memory fallback.
    /// </summary>
    [Benchmark(Description = "Circuit breaker (OPEN - fast fail)", Setup = nameof(SetupOpen))]
    public async Task CircuitBreakerOpenState()
    {
        await _circuitBreaker.PublishAsync("payment.create", new { Amount = 100.0 }, CancellationToken.None);
    }

    /// <summary>
    /// Measure: Concurrent publish operations through circuit breaker.
    /// Tests thread-safety overhead with 10 concurrent tasks.
    /// </summary>
    [Benchmark(Description = "Concurrent publishes (10 parallel)")]
    public async Task ConcurrentPublish()
    {
        var tasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = _circuitBreaker.PublishAsync(
                "payment.create",
                new { Amount = 100.0 + i },
                CancellationToken.None);
        }
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Measure: Concurrent publish with mixed success/failure.
    /// </summary>
    [Benchmark(Description = "Concurrent with failures (5 success, 5 fail)")]
    [IterationSetup(Target = nameof(ConcurrentWithFailures))]
    public void SetupMixedResults()
    {
        _innerPublisher.Reset();
        var callCount = 0;
        _innerPublisher
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount % 2 == 0)
                    throw new InvalidOperationException("DDS error");
                return Task.CompletedTask;
            });

        _circuitBreaker = new CircuitBreakerPublisher(
            _innerPublisher.Object,
            _logger,
            _options);
    }

    public async Task ConcurrentWithFailures()
    {
        var tasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            var idx = i;
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    await _circuitBreaker.PublishAsync(
                        "payment.create",
                        new { Amount = 100.0 + idx },
                        CancellationToken.None);
                }
                catch { /* Expected */ }
            });
        }
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Measure: Fallback mechanism latency (in-memory publish).
    /// When circuit is open, messages are logged to in-memory store.
    /// </summary>
    [Benchmark(Description = "Fallback publish latency")]
    public async Task FallbackPublish()
    {
        // Setup circuit in OPEN state
        _innerPublisher.Reset();
        _innerPublisher
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("DDS unavailable"));

        var cb = new CircuitBreakerPublisher(_innerPublisher.Object, _logger, _options);

        // Trigger failures to open
        for (int i = 0; i < 5; i++)
        {
            try
            {
                await cb.PublishAsync("payment.create", new { Amount = 100.0 }, CancellationToken.None);
            }
            catch { }
        }

        // Now measure fallback publish latency
        await cb.PublishAsync("payment.create", new { Amount = 100.0 }, CancellationToken.None);
    }
}
