using System.Diagnostics;
using System.Text.Json;
using PaymentGateway.Application.Services;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Infrastructure.Observability;

namespace PaymentGateway.Infrastructure.DDS;

public class InMemoryDdsPublisher : IDdsPublisher
{
    private readonly Dictionary<string, List<object>> _messages = new();
    private readonly object _syncRoot = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly bool _logToConsole;

    public InMemoryDdsPublisher(bool logToConsole = true)
    {
        _logToConsole = logToConsole;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public Task PublishAsync<T>(string topic, T data, CancellationToken ct = default) where T : class
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "dds",
            "publish",
            ActivityKind.Producer,
            ("topic", topic),
            ("transport", "inmemory"));

        lock (_syncRoot)
        {
            if (!_messages.ContainsKey(topic))
                _messages[topic] = new List<object>();

            _messages[topic].Add(data!);
        }

        if (_logToConsole)
        {
            var json = JsonSerializer.Serialize(SensitiveDataSanitizer.Sanitize(data), _jsonOptions);
            Console.WriteLine($"[DDS] Published to {topic}: {json}");
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<object> GetMessages(string topic)
    {
        lock (_syncRoot)
        {
            return _messages.TryGetValue(topic, out var messages)
                ? messages.ToList().AsReadOnly()
                : Array.Empty<object>();
        }
    }

    public IReadOnlyList<object> DequeueMessages(string topic)
    {
        lock (_syncRoot)
        {
            if (!_messages.TryGetValue(topic, out var messages) || messages.Count == 0)
                return Array.Empty<object>();

            var snapshot = messages.ToList().AsReadOnly();
            messages.Clear();
            return snapshot;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _messages.Clear();
        }
    }
}
