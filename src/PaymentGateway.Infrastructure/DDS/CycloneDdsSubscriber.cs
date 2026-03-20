using System.Collections.Concurrent;
using System.Diagnostics;
using CycloneDDS.Runtime;
using Microsoft.Extensions.Logging;
using PaymentGateway.Domain.Observability;

namespace PaymentGateway.Infrastructure.DDS;

/// <summary>
/// Optimized CycloneDDS subscriber for PaymentGateway services.
///
/// Performance improvements:
/// - ConcurrentBag eliminates lock contention on reader/task lists
/// - Topic name caching avoids string.Replace() per subscription
/// - Telemetry sampling reduces overhead (configurable)
///
/// Uses DdsReader&lt;T&gt; in real mode.
/// When configured for real DDS mode, initialization/subscription failures are fail-fast.
/// </summary>
public sealed class CycloneDdsSubscriber : IDisposable
{
    // OPTIMIZATION: Cache normalized topic names (same as Publisher)
    private static readonly ConcurrentDictionary<string, string> TopicNameCache =
        new(StringComparer.Ordinal);

    private readonly ILogger<CycloneDdsSubscriber>? _logger;

    // OPTIMIZATION: Use ConcurrentBag instead of List + lock for lock-free access
    private readonly ConcurrentBag<IDisposable> _readers = new();
    private readonly ConcurrentBag<CancellationTokenSource> _subscriptionTokens = new();
    private readonly ConcurrentBag<Task> _subscriptionTasks = new();

    private readonly CancellationTokenSource _shutdownToken = new();
    private DdsParticipant? _participant;
    private readonly bool _useRealDds;

    // OPTIMIZATION: Configurable telemetry sampling (default 10%)
    private readonly float _telemetrySamplingRate;

    private bool _disposed;
    private int _messageCount;

    public CycloneDdsSubscriber(
        ILogger<CycloneDdsSubscriber>? logger = null,
        bool useRealDds = true,
        float telemetrySamplingRate = 0.1f)
    {
        _logger = logger;
        _useRealDds = useRealDds;
        _telemetrySamplingRate = Math.Clamp(telemetrySamplingRate, 0f, 1f);

        if (!useRealDds)
        {
            Console.WriteLine("[CycloneDdsSubscriber] Initialized (fallback mode)");
            return;
        }

        try
        {
            _participant = new DdsParticipant();
            _logger?.LogInformation(
                "[CycloneDdsSubscriber] Initialized with real CycloneDDS (sampling rate: {SamplingRate})",
                _telemetrySamplingRate);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CycloneDdsSubscriber] Failed to initialize CycloneDDS");

            // Check if native library is missing (common Linux issue)
            if (ex.Message.Contains("dll") || ex.Message.Contains("so") ||
                ex.InnerException?.Message.Contains("native") == true)
            {
                _logger?.LogError(
                    "Native DDS library not found. Set CYCLONEDDS_NATIVE_DIR environment variable or add libddsc.so to LD_LIBRARY_PATH");
            }

            throw new InvalidOperationException(
                "CycloneDDS subscriber failed to initialize. Ensure native library is in LD_LIBRARY_PATH (Linux) or system PATH (Windows).",
                ex);
        }
    }

    /// <summary>
    /// Subscribes to a DDS topic and starts asynchronous message processing.
    /// </summary>
    public Task SubscribeAsync<T>(string topic, Func<T, Task> handler, CancellationToken ct = default) where T : struct
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic is required", nameof(topic));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        if (_useRealDds)
        {
            SubscribeToDds(topic, handler, ct);
            return Task.CompletedTask;
        }

        Console.WriteLine($"[DDS] Would subscribe to {topic} (fallback mode)");
        _logger?.LogInformation("Subscribed to {Topic} in fallback mode", topic);
        return Task.CompletedTask;
    }

    private void SubscribeToDds<T>(string topic, Func<T, Task> handler, CancellationToken ct) where T : struct
    {
        if (_participant == null)
            throw new InvalidOperationException("DDS participant is not initialized.");

        // OPTIMIZATION: Use cached topic name to avoid repeated string.Replace() allocations
        var normalizedTopic = GetOrCreateNormalizedTopicName(topic);
        var reader = new DdsReader<T>(_participant, normalizedTopic);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownToken.Token, ct);

        // OPTIMIZATION: ConcurrentBag is lock-free, no need for explicit lock
        _readers.Add(reader);
        _subscriptionTokens.Add(linkedCts);

        var task = Task.Run(async () =>
        {
            _logger?.LogInformation(
                "Subscribed to DDS topic {Topic} (normalized: {NormalizedTopic}) with payload type {PayloadType}",
                topic,
                normalizedTopic,
                typeof(T).Name);

            try
            {
                await foreach (var message in reader.StreamAsync(linkedCts.Token))
                {
                    // OPTIMIZATION: Sample telemetry to reduce overhead (default 10% sampling)
                    var shouldTraceTelemetry = _telemetrySamplingRate >= 1.0f ||
                        (Random.Shared.NextSingle() < _telemetrySamplingRate);

                    PaymentGatewayTelemetry.TelemetryOperation? operation = null;
                    try
                    {
                        if (shouldTraceTelemetry)
                        {
                            operation = PaymentGatewayTelemetry.StartOperation(
                                "dds",
                                "consume",
                                ActivityKind.Consumer,
                                ("topic", topic),
                                ("transport", "cyclonedds"),
                                ("payload.type", typeof(T).Name));
                        }

                        await handler(message);
                    }
                    catch (Exception ex)
                    {
                        operation?.RecordException(ex);
                        throw;
                    }
                    finally
                    {
                        operation?.Dispose();
                        Interlocked.Increment(ref _messageCount);
                    }
                }
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
            {
                // graceful cancellation
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while reading DDS topic {Topic}", topic);
            }
        }, CancellationToken.None);

        // OPTIMIZATION: ConcurrentBag is lock-free
        _subscriptionTasks.Add(task);
    }

    /// <summary>
    /// Get or create a normalized topic name.
    /// OPTIMIZATION: Static cache shared across all subscriber instances (same as Publisher).
    /// </summary>
    private static string GetOrCreateNormalizedTopicName(string topic)
    {
        return TopicNameCache.GetOrAdd(topic, t => t.Replace('.', '_'));
    }

    /// <summary>
    /// Starts multiple subscriptions and returns when all registrations are complete.
    /// </summary>
    public async Task StartSubscriptionsAsync<T>(IDictionary<string, Func<T, Task>> subscriptions, CancellationToken ct = default)
        where T : struct
    {
        foreach (var subscription in subscriptions)
        {
            await SubscribeAsync(subscription.Key, subscription.Value, ct);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _shutdownToken.Cancel();

        // OPTIMIZATION: ConcurrentBag doesn't need lock for enumeration
        foreach (var token in _subscriptionTokens)
            token.Dispose();

        foreach (var reader in _readers)
            reader.Dispose();

        _participant?.Dispose();
        _participant = null;
        _shutdownToken.Dispose();

        _logger?.LogInformation(
            "[CycloneDdsSubscriber] Disposed. Processed {MessageCount} messages.",
            _messageCount);
    }
}
