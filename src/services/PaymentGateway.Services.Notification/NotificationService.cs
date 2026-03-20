using System.Diagnostics;
using System.Text.Json;
using PaymentGateway.Application.Services;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Infrastructure.Configuration;
using PaymentGateway.Infrastructure.DDS;
using PaymentGateway.Infrastructure.DDS.DdsTypes;
using PaymentGateway.Infrastructure.Observability;
using PaymentGateway.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


public class NotificationService
{
    private readonly IWebhookRepository _webhookRepository;
    private readonly IDdsPublisher _ddsPublisher;
    private readonly CycloneDdsSubscriber? _ddsSubscriber;
    private readonly WebhookDispatcher _webhookDispatcher;
    private readonly ILogger<NotificationService> _logger;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public NotificationService(
        IWebhookRepository webhookRepository,
        IDdsPublisher ddsPublisher,
        WebhookDispatcher webhookDispatcher,
        ILogger<NotificationService> logger,
        CycloneDdsSubscriber? ddsSubscriber = null)
    {
        _webhookRepository = webhookRepository;
        _ddsPublisher = ddsPublisher;
        _ddsSubscriber = ddsSubscriber;
        _webhookDispatcher = webhookDispatcher;
        _logger = logger;
    }

    public Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        if (_ddsSubscriber != null)
        {
            _listenerTask = ListenForNotificationsViaRealDdsAsync(_cts.Token);
            _logger.LogInformation("Listening for notification events using real CycloneDDS topics...");
        }
        else
        {
            _listenerTask = ListenForNotificationsInMemoryAsync(_cts.Token);
            _logger.LogInformation("Listening for notification events in in-memory mode...");
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_listenerTask != null)
            await _listenerTask;
    }

    private async Task ListenForNotificationsViaRealDdsAsync(CancellationToken ct)
    {
        if (_ddsSubscriber == null)
            return;

        await _ddsSubscriber.SubscribeAsync<PaymentCreatedEvent>(
            "payment.created",
            message => SendPaymentNotificationAsync(message, "payment.created", ct),
            ct);

        await _ddsSubscriber.SubscribeAsync<PaymentApprovedEvent>(
            "payment.approved",
            message => SendPaymentNotificationAsync(message, "payment.approved", ct),
            ct);

        await _ddsSubscriber.SubscribeAsync<PaymentRejectedEvent>(
            "payment.rejected",
            message => SendPaymentNotificationAsync(message, "payment.rejected", ct),
            ct);

        await _ddsSubscriber.SubscribeAsync<PaymentRefundedEvent>(
            "payment.refunded",
            message => SendPaymentNotificationAsync(message, "payment.refunded", ct),
            ct);

        await _ddsSubscriber.SubscribeAsync<PaymentCapturedEvent>(
            "payment.captured",
            message => SendPaymentNotificationAsync(message, "payment.captured", ct),
            ct);

        await _ddsSubscriber.SubscribeAsync<PaymentCancelledEvent>(
            "payment.cancelled",
            message => SendPaymentNotificationAsync(message, "payment.cancelled", ct),
            ct);

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    private async Task ListenForNotificationsInMemoryAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_ddsPublisher is InMemoryDdsPublisher inMemory)
                {
                    // Listen for various payment events
                    var createdEvents = inMemory.DequeueMessages("payment.created");
                    var approvedEvents = inMemory.DequeueMessages("payment.approved");
                    var rejectedEvents = inMemory.DequeueMessages("payment.rejected");
                    var refundedEvents = inMemory.DequeueMessages("payment.refunded");
                    var capturedEvents = inMemory.DequeueMessages("payment.captured");
                    var cancelledEvents = inMemory.DequeueMessages("payment.cancelled");

                    foreach (var msg in createdEvents)
                    {
                        await SendPaymentNotificationAsync(msg, "payment.created", ct);
                    }

                    foreach (var msg in approvedEvents)
                    {
                        await SendPaymentNotificationAsync(msg, "payment.approved", ct);
                    }

                    foreach (var msg in rejectedEvents)
                    {
                        await SendPaymentNotificationAsync(msg, "payment.rejected", ct);
                    }

                    foreach (var msg in refundedEvents)
                    {
                        await SendPaymentNotificationAsync(msg, "payment.refunded", ct);
                    }

                    foreach (var msg in capturedEvents)
                    {
                        await SendPaymentNotificationAsync(msg, "payment.captured", ct);
                    }

                    foreach (var msg in cancelledEvents)
                    {
                        await SendPaymentNotificationAsync(msg, "payment.cancelled", ct);
                    }
                }

                await Task.Delay(100, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in notification listener");
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task SendPaymentNotificationAsync(object message, string eventType, CancellationToken ct)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "notification",
            "dispatch_event",
            ActivityKind.Consumer,
            ("event.type", eventType));

        try
        {
            _logger.LogInformation(
                "Processing notification for {EventType} with payload {@Payload}",
                eventType,
                SensitiveDataSanitizer.Sanitize(message));

            // Extract payment data
            var paymentData = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(message));
            if (paymentData == null) return;

            var merchantId = Guid.TryParse(paymentData["merchantId"]?.ToString(), out var mid) ? mid : Guid.Empty;
            if (merchantId == Guid.Empty) return;

            // Get merchant webhooks
            var webhooks = await _webhookRepository.GetByMerchantAsync(merchantId, ct);

            foreach (var webhook in webhooks)
            {
                try
                {
                    await _webhookDispatcher.DispatchAsync(webhook, eventType, message, ct);
                    _logger.LogInformation("Webhook {WebhookId} dispatched for {EventType}", webhook.Id, eventType);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to dispatch webhook {WebhookId}", webhook.Id);
                }
            }
        }
        catch (Exception ex)
        {
            operation.RecordException(ex);
            _logger.LogError(ex, "Error sending payment notification");
        }
    }

    private Task SendPaymentNotificationAsync(PaymentCreatedEvent message, string eventType, CancellationToken ct)
    {
        var payload = new
        {
            paymentId = message.PaymentId,
            merchantId = message.MerchantId,
            amount = message.Amount,
            currency = message.Currency,
            status = message.Status,
            createdAt = message.CreatedAt
        };

        return SendPaymentNotificationAsync(payload, eventType, ct);
    }

    private Task SendPaymentNotificationAsync(PaymentApprovedEvent message, string eventType, CancellationToken ct)
    {
        var payload = new
        {
            paymentId = message.PaymentId,
            merchantId = message.MerchantId,
            amount = message.Amount,
            currency = message.Currency,
            transactionId = message.TransactionId,
            processedAt = message.ProcessedAt
        };

        return SendPaymentNotificationAsync(payload, eventType, ct);
    }

    private Task SendPaymentNotificationAsync(PaymentRejectedEvent message, string eventType, CancellationToken ct)
    {
        var payload = new
        {
            paymentId = message.PaymentId,
            merchantId = message.MerchantId,
            reason = message.Reason,
            processedAt = message.ProcessedAt
        };

        return SendPaymentNotificationAsync(payload, eventType, ct);
    }

    private Task SendPaymentNotificationAsync(PaymentRefundedEvent message, string eventType, CancellationToken ct)
    {
        var payload = new
        {
            paymentId = message.PaymentId,
            merchantId = message.MerchantId,
            amount = message.Amount,
            currency = message.Currency,
            reason = message.Reason,
            refundedAt = message.RefundedAt
        };

        return SendPaymentNotificationAsync(payload, eventType, ct);
    }

    private Task SendPaymentNotificationAsync(PaymentCapturedEvent message, string eventType, CancellationToken ct)
    {
        var payload = new
        {
            paymentId = message.PaymentId,
            merchantId = message.MerchantId,
            amount = message.Amount,
            currency = message.Currency,
            capturedAt = message.CapturedAt
        };

        return SendPaymentNotificationAsync(payload, eventType, ct);
    }

    private Task SendPaymentNotificationAsync(PaymentCancelledEvent message, string eventType, CancellationToken ct)
    {
        var payload = new
        {
            paymentId = message.PaymentId,
            merchantId = message.MerchantId,
            reason = message.Reason,
            cancelledAt = message.CancelledAt
        };

        return SendPaymentNotificationAsync(payload, eventType, ct);
    }
}
