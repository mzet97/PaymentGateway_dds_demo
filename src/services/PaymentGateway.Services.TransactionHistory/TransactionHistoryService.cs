using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Infrastructure.Configuration;
using PaymentGateway.Infrastructure.DDS;
using PaymentGateway.Infrastructure.DDS.DdsTypes;
using PaymentGateway.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
namespace PaymentGateway.Services.TransactionHistory;

public class TransactionHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CycloneDdsSubscriber _subscriber;
    private readonly ITransactionEventRepository _eventRepository;
    private readonly ILogger<TransactionHistoryService> _logger;

    public TransactionHistoryService(
        CycloneDdsSubscriber subscriber,
        ITransactionEventRepository eventRepository,
        ILogger<TransactionHistoryService> logger)
    {
        _subscriber = subscriber;
        _eventRepository = eventRepository;
        _logger = logger;
    }

    public async Task SubscribeToEventsAsync()
    {
        _logger.LogInformation("Subscribing to payment events...");

        await _subscriber.SubscribeAsync<PaymentCreateCommand>("payment.create", HandlePaymentCreateAsync);
        await _subscriber.SubscribeAsync<PaymentCreatedEvent>("payment.created", HandlePaymentCreatedAsync);
        await _subscriber.SubscribeAsync<PaymentApprovedEvent>("payment.approved", HandlePaymentApprovedAsync);
        await _subscriber.SubscribeAsync<PaymentRejectedEvent>("payment.rejected", HandlePaymentRejectedAsync);
        await _subscriber.SubscribeAsync<PaymentCapturedEvent>("payment.captured", HandlePaymentCapturedAsync);
        await _subscriber.SubscribeAsync<PaymentRefundedEvent>("payment.refunded", HandlePaymentRefundedAsync);
        await _subscriber.SubscribeAsync<PaymentCancelledEvent>("payment.cancelled", HandlePaymentCancelledAsync);

        _logger.LogInformation("Subscribed to payment events");
    }

    public async Task StoreEventAsync<T>(string eventType, Guid paymentId, T eventData)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "history",
            "store_event",
            ActivityKind.Consumer,
            ("event.type", eventType),
            ("payment.id", paymentId.ToString()));

        var payload = JsonSerializer.Serialize(eventData, JsonOptions);
        var merchantId = ExtractMerchantId(payload);

        await _eventRepository.StoreAsync(new StoredTransactionEvent
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentId,
            MerchantId = merchantId,
            EventType = eventType,
            PayloadJson = payload,
            OccurredAt = DateTime.UtcNow
        });

        PaymentGatewayTelemetry.RecordPaymentLifecycle(eventType, NormalizeStatus(eventType));
        _logger.LogInformation("Stored event {EventType} for payment {PaymentId}", eventType, paymentId);
    }

    public async Task<List<T>> GetEventsForPaymentAsync<T>(Guid paymentId) where T : class
    {
        var events = await _eventRepository.GetByPaymentAsync(paymentId);
        var result = new List<T>();

        foreach (var evt in events)
        {
            var deserialized = JsonSerializer.Deserialize<T>(evt.PayloadJson, JsonOptions);
            if (deserialized != null)
            {
                result.Add(deserialized);
            }
        }

        return result;
    }

    private Task HandlePaymentCreateAsync(PaymentCreateCommand message)
    {
        return StoreEventAsync("payment.create", message.PaymentId, message);
    }

    private Task HandlePaymentCreatedAsync(PaymentCreatedEvent message)
    {
        return StoreEventAsync("payment.created", message.PaymentId, message);
    }

    private Task HandlePaymentApprovedAsync(PaymentApprovedEvent message)
    {
        return StoreEventAsync("payment.approved", message.PaymentId, message);
    }

    private Task HandlePaymentRejectedAsync(PaymentRejectedEvent message)
    {
        return StoreEventAsync("payment.rejected", message.PaymentId, message);
    }

    private Task HandlePaymentCapturedAsync(PaymentCapturedEvent message)
    {
        return StoreEventAsync("payment.captured", message.PaymentId, message);
    }

    private Task HandlePaymentRefundedAsync(PaymentRefundedEvent message)
    {
        return StoreEventAsync("payment.refunded", message.PaymentId, message);
    }

    private Task HandlePaymentCancelledAsync(PaymentCancelledEvent message)
    {
        return StoreEventAsync("payment.cancelled", message.PaymentId, message);
    }

    public async Task<TransactionAnalytics> GetAnalyticsAsync(DateTime from, DateTime to, string groupBy)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "history",
            "analytics",
            ActivityKind.Internal,
            ("group.by", groupBy));

        _logger.LogInformation("Getting analytics from {From} to {To}, grouped by {GroupBy}", from, to, groupBy);

        var events = await _eventRepository.GetByDateRangeAsync(from, to);
        var createdEvents = events
            .Where(e => e.EventType is "payment.create" or "payment.created")
            .ToList();

        var totalTransactions = createdEvents
            .Select(e => e.PaymentId)
            .Distinct()
            .Count();

        var totalAmount = createdEvents
            .Select(ExtractAmount)
            .Sum();

        var averageAmount = totalTransactions == 0
            ? 0m
            : totalAmount / totalTransactions;

        var byStatus = events
            .Where(e => e.EventType.StartsWith("payment.", StringComparison.Ordinal))
            .GroupBy(e => NormalizeStatus(e.EventType))
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var byMethod = createdEvents
            .Select(ExtractMethod)
            .Where(method => !string.IsNullOrWhiteSpace(method))
            .GroupBy(method => method!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return new TransactionAnalytics
        {
            TotalTransactions = totalTransactions,
            TotalAmount = totalAmount,
            AverageAmount = averageAmount,
            ByStatus = byStatus,
            ByMethod = byMethod
        };
    }

    public void Stop()
    {
        _subscriber.Dispose();
    }

    private static Guid ExtractMerchantId(string payloadJson)
    {
        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(payloadJson);
            if (element.ValueKind == JsonValueKind.Object &&
                TryGetProperty(element, "merchantId", out var merchantElement) &&
                merchantElement.ValueKind == JsonValueKind.String &&
                Guid.TryParse(merchantElement.GetString(), out var merchantId))
            {
                return merchantId;
            }
        }
        catch
        {
            // best-effort metadata extraction
        }

        return Guid.Empty;
    }

    private static decimal ExtractAmount(StoredTransactionEvent evt)
    {
        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(evt.PayloadJson);
            if (element.ValueKind != JsonValueKind.Object)
                return 0m;

            if (!TryGetProperty(element, "amount", out var amountElement))
                return 0m;

            if (amountElement.ValueKind == JsonValueKind.Number && amountElement.TryGetDecimal(out var amount))
                return amount;

            if (amountElement.ValueKind == JsonValueKind.String &&
                decimal.TryParse(amountElement.GetString(), out var parsedAmount))
                return parsedAmount;
        }
        catch
        {
            // ignore malformed payload
        }

        return 0m;
    }

    private static string? ExtractMethod(StoredTransactionEvent evt)
    {
        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(evt.PayloadJson);
            if (element.ValueKind != JsonValueKind.Object)
                return null;

            if (TryGetProperty(element, "method", out var methodElement))
                return methodElement.GetString();
        }
        catch
        {
            // ignore malformed payload
        }

        return null;
    }

    private static string NormalizeStatus(string eventType)
    {
        return eventType switch
        {
            "payment.create" => "pending",
            "payment.created" => "created",
            "payment.approved" => "approved",
            "payment.rejected" => "rejected",
            "payment.captured" => "captured",
            "payment.refunded" => "refunded",
            "payment.cancelled" => "cancelled",
            _ => eventType
        };
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

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
