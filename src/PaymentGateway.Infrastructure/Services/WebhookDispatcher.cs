using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Observability;

namespace PaymentGateway.Infrastructure.Services;

public class WebhookDispatcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDispatcher> _logger;
    private readonly int MaxRetries = 3;
    private readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public WebhookDispatcher(IHttpClientFactory httpClientFactory, ILogger<WebhookDispatcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task DispatchAsync(Webhook webhook, string eventType, object payload, CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "webhook",
            "dispatch",
            ActivityKind.Client,
            ("webhook.id", webhook.Id.ToString()),
            ("merchant.id", webhook.MerchantId.ToString()),
            ("event.type", eventType));

        if (!webhook.IsActive)
        {
            operation.SetResult("skipped_inactive");
            _logger.LogDebug("Webhook {WebhookId} is not active, skipping", webhook.Id);
            return;
        }

        if (!webhook.Events.Contains(eventType) && !webhook.Events.Contains("*"))
        {
            operation.SetResult("skipped_unsubscribed");
            _logger.LogDebug("Webhook {WebhookId} not subscribed to event {EventType}, skipping", webhook.Id, eventType);
            return;
        }

        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
        var signature = GenerateSignature(payloadJson, webhook.Secret);

        var client = _httpClientFactory.CreateClient("webhook");
        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
                {
                    Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("X-Webhook-Signature", signature);
                request.Headers.Add("X-Webhook-Event", eventType);
                request.Headers.Add("X-Webhook-Id", webhook.Id.ToString());

                using var response = await client.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                {
                    operation.SetResult("success");
                    _logger.LogInformation("Webhook {WebhookId} delivered successfully for event {EventType}", webhook.Id, eventType);
                    return;
                }

                lastException = new HttpRequestException($"Webhook returned status {(int)response.StatusCode}");
                _logger.LogWarning("Webhook {WebhookId} returned {StatusCode}, attempt {Attempt}/{MaxRetries}",
                    webhook.Id, response.StatusCode, attempt, MaxRetries);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Webhook {WebhookId} delivery failed, attempt {Attempt}/{MaxRetries}",
                    webhook.Id, attempt, MaxRetries);
            }

            if (attempt < MaxRetries)
            {
                await Task.Delay(RetryDelay * attempt, ct);
            }
        }

        if (lastException != null)
        {
            operation.RecordException(lastException);
        }

        _logger.LogError(lastException, "Webhook {WebhookId} failed after {MaxRetries} attempts for event {EventType}",
            webhook.Id, MaxRetries, eventType);

        throw new InvalidOperationException($"Failed to deliver webhook after {MaxRetries} attempts", lastException);
    }

    public static string GenerateSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }
}
