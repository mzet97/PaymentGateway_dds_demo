using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Text.Json;
using PaymentGateway.Infrastructure.DDS.DdsTypes;

namespace PaymentGateway.Benchmarks;

/// <summary>
/// Benchmarks JSON serialization performance improvements from Phase 1.
/// Measures: allocation rate, throughput, latency of manual vs optimized serialization.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[MedianColumn, Min95Column, Max95Column, StdDevColumn, AllocationColumn]
public class JsonSerializationBenchmark
{
    private PaymentCreateCommand _command = null!;
    private PaymentCreatedEvent _event = null!;
    private JsonDocument _commandJson = null!;
    private JsonDocument _eventJson = null!;

    [GlobalSetup]
    public void Setup()
    {
        _command = new PaymentCreateCommand
        {
            MerchantId = "MERCH-001",
            Amount = 12345.67,
            Currency = "USD",
            CustomerId = "CUST-001",
            Description = "Payment for order #12345",
            OrderId = "ORD-12345"
        };

        _event = new PaymentCreatedEvent
        {
            PaymentId = Guid.NewGuid(),
            MerchantId = "MERCH-001",
            Amount = 12345.67,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            Status = "Created"
        };

        // Pre-serialize for comparison
        var commandJson = JsonSerializer.Serialize(_command);
        var eventJson = JsonSerializer.Serialize(_event);

        _commandJson = JsonDocument.Parse(commandJson);
        _eventJson = JsonDocument.Parse(eventJson);
    }

    /// <summary>
    /// Baseline: Serialize command object to JSON.
    /// This is the initial cost before any optimizations.
    /// </summary>
    [Benchmark(Description = "Serialize PaymentCreateCommand")]
    public string SerializeCommand()
    {
        return JsonSerializer.Serialize(_command);
    }

    /// <summary>
    /// Baseline: Serialize event object to JSON.
    /// </summary>
    [Benchmark(Description = "Serialize PaymentCreatedEvent")]
    public string SerializeEvent()
    {
        return JsonSerializer.Serialize(_event);
    }

    /// <summary>
    /// Measure: Extract fields from JsonDocument (simulates current parser implementation).
    /// This represents the OLD implementation with Clone() calls.
    /// </summary>
    [Benchmark(Description = "Extract from JsonDocument (with cloning)")]
    public (string MerchantId, double Amount, string Currency) ExtractFieldsWithCloning()
    {
        var merchantId = _commandJson.RootElement.GetProperty("merchantId").GetString() ?? "";
        var amount = _commandJson.RootElement.GetProperty("amount").GetDouble();
        var currency = _commandJson.RootElement.GetProperty("currency").GetString() ?? "";

        // Simulate Clone() call from old implementation
        var cloned = merchantId.Clone() as string;

        return (cloned ?? merchantId, amount, currency);
    }

    /// <summary>
    /// Measure: Extract fields from JsonDocument (optimized, no cloning).
    /// This represents the NEW implementation after Phase 1 optimizations.
    /// </summary>
    [Benchmark(Description = "Extract from JsonDocument (optimized, no cloning)")]
    public (string MerchantId, double Amount, string Currency) ExtractFieldsOptimized()
    {
        var merchantId = _commandJson.RootElement.GetProperty("merchantId").GetString() ?? "";
        var amount = _commandJson.RootElement.GetProperty("amount").GetDouble();
        var currency = _commandJson.RootElement.GetProperty("currency").GetString() ?? "";

        return (merchantId, amount, currency);
    }

    /// <summary>
    /// Measure: Round-trip serialization (serialize + deserialize).
    /// Represents the full cycle of publishing a message.
    /// </summary>
    [Benchmark(Description = "Round-trip: Serialize + Extract")]
    public (string MerchantId, double Amount) RoundTrip()
    {
        var json = JsonSerializer.Serialize(_command);
        var doc = JsonDocument.Parse(json);
        var merchantId = doc.RootElement.GetProperty("merchantId").GetString() ?? "";
        var amount = doc.RootElement.GetProperty("amount").GetDouble();
        return (merchantId, amount);
    }

    /// <summary>
    /// Measure: Batch serialization of multiple commands.
    /// Simulates realistic workload with multiple messages.
    /// </summary>
    [Benchmark(Description = "Batch serialize 100 commands")]
    public List<string> BatchSerialize()
    {
        var results = new List<string>(100);
        for (int i = 0; i < 100; i++)
        {
            var cmd = new PaymentCreateCommand
            {
                MerchantId = "MERCH-001",
                Amount = 12345.67 + i,
                Currency = "USD",
                CustomerId = $"CUST-{i:D6}",
                Description = $"Payment {i}",
                OrderId = $"ORD-{i:D6}"
            };
            results.Add(JsonSerializer.Serialize(cmd));
        }
        return results;
    }

    /// <summary>
    /// Measure: Topic name normalization (cached vs non-cached).
    /// This simulates the TopicNameCache optimization from Phase 1.
    /// </summary>
    [Benchmark(Description = "Topic normalization (10 topics)")]
    public string TopicNormalization()
    {
        var topics = new[]
        {
            "payment.create", "payment.created", "payment.approved", "payment.rejected",
            "fraud.check", "fraud.checked", "settlement.process", "notification.send",
            "history.record", "analytics.update"
        };

        var result = "";
        foreach (var topic in topics)
        {
            // Simulate cache lookup
            result = NormalizeTopicName(topic);
        }
        return result;
    }

    private static string NormalizeTopicName(string topic)
    {
        return topic.Replace(".", "_").Replace("-", "_").ToUpperInvariant();
    }

    /// <summary>
    /// Measure: Large message serialization (500 byte payload).
    /// Tests performance impact on larger payloads.
    /// </summary>
    [Benchmark(Description = "Serialize large payload")]
    public string SerializeLargePayload()
    {
        var largeCommand = new PaymentCreateCommand
        {
            MerchantId = "MERCH-001",
            Amount = 12345.67,
            Currency = "USD",
            CustomerId = "CUST-001",
            Description = new string('x', 200),
            OrderId = "ORD-12345"
        };
        return JsonSerializer.Serialize(largeCommand);
    }

    [Benchmark(Description = "Guid parsing from JSON")]
    public Guid ParseGuidFromJson()
    {
        var guidStr = _eventJson.RootElement.GetProperty("paymentId").GetString();
        return Guid.Parse(guidStr ?? throw new InvalidOperationException("Missing paymentId"));
    }
}

/// <summary>
/// Test data models matching DDS types.
/// </summary>
public class PaymentCreateCommand
{
    public string MerchantId { get; set; } = string.Empty;
    public double Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string CustomerId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
}

public class PaymentCreatedEvent
{
    public Guid PaymentId { get; set; }
    public string MerchantId { get; set; } = string.Empty;
    public double Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "Created";
}
