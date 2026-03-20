using System.Text.Json;
using PaymentGateway.Application.Services;

namespace PaymentGateway.Api.Common.Realtime;

public sealed class BroadcastingDdsPublisher : IDdsPublisher
{
    private readonly IDdsPublisher _inner;
    private readonly IPaymentUpdatesNotifier _notifier;
    private readonly ILogger<BroadcastingDdsPublisher> _logger;

    public BroadcastingDdsPublisher(
        IDdsPublisher inner,
        IPaymentUpdatesNotifier notifier,
        ILogger<BroadcastingDdsPublisher> logger)
    {
        _inner = inner;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task PublishAsync<T>(string topic, T data, CancellationToken ct = default) where T : class
    {
        await _inner.PublishAsync(topic, data, ct);

        if (!topic.StartsWith("payment.", StringComparison.Ordinal))
            return;

        var merchantId = ExtractMerchantId(data);
        if (merchantId == Guid.Empty)
            return;

        try
        {
            await _notifier.BroadcastAsync(merchantId, topic, data, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast realtime payment update for topic {Topic}", topic);
        }
    }

    private static Guid ExtractMerchantId<T>(T payload) where T : class
    {
        try
        {
            var element = JsonSerializer.SerializeToElement(payload);
            if (element.ValueKind != JsonValueKind.Object)
                return Guid.Empty;

            if (!TryGetProperty(element, "merchantId", out var merchantElement))
                return Guid.Empty;

            if (merchantElement.ValueKind == JsonValueKind.String &&
                Guid.TryParse(merchantElement.GetString(), out var merchantId))
            {
                return merchantId;
            }
        }
        catch
        {
            // no-op, best-effort extraction
        }

        return Guid.Empty;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
