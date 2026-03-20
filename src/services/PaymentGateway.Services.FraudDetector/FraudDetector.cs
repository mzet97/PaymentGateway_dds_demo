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
using PaymentGateway.Domain.ValueObjects;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

/// <summary>
/// DDS-based fraud detection service that analyzes payment transactions using AI (OpenRouter/MiniMax M2.5).
///
/// Subscribes to DDS topic "fraud.check" and publishes results to "fraud.checked".
///
/// Fraud Risk Score Scale (0-100):
///   0-25  → Approved  — Transaction is legitimate, low risk. Auto-approved.
///   26-60 → Review    — Suspicious, requires manual review. Payment stays pending.
///   61-100→ Rejected  — High probability of fraud. Auto-rejected.
///
/// The AI considers: transaction amount, email patterns, document validity,
/// IP geolocation, and known fraud indicators.
///
/// Flow: API → DDS(payment.create) → PaymentProcessor → DDS(fraud.check) → FraudDetector
///       → OpenRouter AI → DDS(fraud.checked) → PaymentProcessor → MongoDB update
/// </summary>
public class FraudDetector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true
    };

    private readonly IPaymentRepository _paymentRepository;
    private readonly IDdsPublisher _ddsPublisher;
    private readonly CycloneDdsSubscriber? _ddsSubscriber;
    private readonly IOpenRouterFraudService _fraudService;
    private readonly ILogger<FraudDetector> _logger;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public FraudDetector(
        IPaymentRepository paymentRepository,
        IDdsPublisher ddsPublisher,
        IOpenRouterFraudService fraudService,
        ILogger<FraudDetector> logger,
        CycloneDdsSubscriber? ddsSubscriber = null)
    {
        _paymentRepository = paymentRepository;
        _ddsPublisher = ddsPublisher;
        _fraudService = fraudService;
        _logger = logger;
        _ddsSubscriber = ddsSubscriber;
    }

    public Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        if (_ddsSubscriber != null)
        {
            _listenerTask = ListenForFraudChecksViaRealDdsAsync(_cts.Token);
            _logger.LogInformation("Listening for fraud.check requests using real CycloneDDS topics...");
        }
        else
        {
            _listenerTask = ListenForFraudChecksInMemoryAsync(_cts.Token);
            _logger.LogInformation("Listening for fraud.check requests in in-memory mode...");
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_listenerTask != null)
            await _listenerTask;
    }

    private async Task ListenForFraudChecksViaRealDdsAsync(CancellationToken ct)
    {
        if (_ddsSubscriber == null)
            return;

        await _ddsSubscriber.SubscribeAsync<FraudCheckCommand>("fraud.check",
            message => AnalyzePaymentAsync(message, ct), ct);

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    private async Task ListenForFraudChecksInMemoryAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_ddsPublisher is InMemoryDdsPublisher inMemory)
                {
                    var messages = inMemory.DequeueMessages("fraud.check");

                    foreach (var msg in messages)
                    {
                        await AnalyzePaymentAsync(msg, ct);
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
                _logger.LogError(ex, "Error in fraud check listener");
                await Task.Delay(1000, ct);
            }
        }
    }

    private Task AnalyzePaymentAsync(FraudCheckCommand message, CancellationToken ct)
    {
        _logger.LogInformation("Analyzing fraud.check from DDS for payment {PaymentId}", message.PaymentId);
        return AnalyzePaymentCoreAsync(
            message.PaymentId,
            (decimal)message.Amount,
            message.Currency,
            message.CustomerEmail,
            message.CustomerDocument,
            message.CustomerIp,
            ct);
    }

    private async Task AnalyzePaymentAsync(object message, CancellationToken ct)
    {
        _logger.LogInformation(
            "Analyzing payment for fraud payload {@Payload}",
            SensitiveDataSanitizer.Sanitize(message));

        Guid paymentId = Guid.Empty;

        try
        {
            var payload = JsonSerializer.SerializeToElement(message, JsonOptions);
            paymentId = payload.TryGetProperty("paymentId", out var idElement) &&
                        idElement.ValueKind == JsonValueKind.String &&
                        Guid.TryParse(idElement.GetString(), out var parsedId)
                ? parsedId
                : Guid.Empty;

            var amount = payload.TryGetProperty("amount", out var amountElement) &&
                         amountElement.TryGetDecimal(out var parsedAmount)
                ? parsedAmount
                : 0m;

            var currency = payload.TryGetProperty("currency", out var currencyElement)
                ? currencyElement.GetString() ?? "USD"
                : "USD";

            string customerEmail = string.Empty;
            string customerDocument = string.Empty;
            string customerIp = string.Empty;

            if (payload.TryGetProperty("customer", out var customerElement) &&
                customerElement.ValueKind == JsonValueKind.Object)
            {
                if (customerElement.TryGetProperty("email", out var email))
                    customerEmail = email.GetString() ?? string.Empty;
                if (customerElement.TryGetProperty("document", out var document))
                    customerDocument = document.GetString() ?? string.Empty;
                if (customerElement.TryGetProperty("ip", out var ip))
                    customerIp = ip.GetString() ?? string.Empty;
            }
            else
            {
                if (payload.TryGetProperty("customerEmail", out var email))
                    customerEmail = email.GetString() ?? string.Empty;
                if (payload.TryGetProperty("customerDocument", out var document))
                    customerDocument = document.GetString() ?? string.Empty;
                if (payload.TryGetProperty("customerIp", out var ip))
                    customerIp = ip.GetString() ?? string.Empty;
            }

            await AnalyzePaymentCoreAsync(
                paymentId,
                amount,
                currency,
                customerEmail,
                customerDocument,
                customerIp,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing fraud");
            await PublishFallbackReviewAsync(paymentId, ct);
        }
    }

    private async Task AnalyzePaymentCoreAsync(
        Guid paymentId,
        decimal amount,
        string currency,
        string customerEmail,
        string customerDocument,
        string customerIp,
        CancellationToken ct)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "fraud",
            "analyze_payment",
            ActivityKind.Internal,
            ("payment.id", paymentId.ToString()),
            ("currency", currency));

        if (paymentId == Guid.Empty)
        {
            operation.SetResult("ignored");
            _logger.LogWarning("Ignoring fraud.check with invalid PaymentId");
            return;
        }

        try
        {
            var result = await _fraudService.AnalyzePaymentAsync(
                paymentId,
                amount,
                currency,
                customerEmail,
                customerDocument,
                customerIp,
                ct);

            await _ddsPublisher.PublishAsync("fraud.checked", new
            {
                paymentId = paymentId,
                riskScore = result.RiskScore,
                decision = result.Decision.ToString(),
                reasons = result.Reasons,
                metadata = new
                {
                    model = result.Metadata?.Model ?? "minimax-m2.5",
                    latency = result.Metadata?.LatencyMs ?? 0,
                    confidence = result.Metadata?.Confidence ?? 0.95m
                },
                timestamp = DateTime.UtcNow
            }, ct);

            _logger.LogInformation(
                "Fraud analysis complete for {PaymentId}: Score={Score}, Decision={Decision}",
                paymentId,
                result.RiskScore,
                result.Decision);

            var payment = await _paymentRepository.GetByIdAsync(paymentId, ct);
            if (payment != null && result.Decision == FraudDecision.Rejected)
            {
                payment.Reject("Fraud detected: " + string.Join(", ", result.Reasons));
                await _paymentRepository.UpdateAsync(payment, ct);
            }
        }
        catch (Exception ex)
        {
            operation.RecordException(ex);
            _logger.LogError(ex, "Error analyzing fraud");
            await PublishFallbackReviewAsync(paymentId, ct);
        }
    }

    private Task PublishFallbackReviewAsync(Guid paymentId, CancellationToken ct)
    {
        return _ddsPublisher.PublishAsync("fraud.checked", new
        {
            paymentId = paymentId,
            riskScore = 50m,
            decision = FraudDecision.Review.ToString(),
            reasons = new[] { "Analysis failed, defaulting to review" },
            metadata = new
            {
                model = "error-fallback",
                latency = 0,
                confidence = 0m
            },
            timestamp = DateTime.UtcNow
        }, ct);
    }
}
