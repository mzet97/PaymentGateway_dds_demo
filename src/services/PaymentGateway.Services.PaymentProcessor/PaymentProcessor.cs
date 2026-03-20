using System.Diagnostics;
using System.Text.Json;
using PaymentGateway.Application.Services;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Domain.ValueObjects;
using PaymentGateway.Infrastructure.Configuration;
using PaymentGateway.Infrastructure.DDS;
using PaymentGateway.Infrastructure.DDS.DdsTypes;
using PaymentGateway.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


public class PaymentProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true
    };

    private readonly IPaymentRepository _paymentRepository;
    private readonly IDdsPublisher _ddsPublisher;
    private readonly CycloneDdsSubscriber? _ddsSubscriber;
    private readonly ITransactionEventRepository? _transactionEventRepository;
    private readonly ILogger<PaymentProcessor> _logger;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public PaymentProcessor(
        IPaymentRepository paymentRepository,
        IDdsPublisher ddsPublisher,
        ILogger<PaymentProcessor> logger,
        CycloneDdsSubscriber? ddsSubscriber = null,
        ITransactionEventRepository? transactionEventRepository = null)
    {
        _paymentRepository = paymentRepository;
        _ddsPublisher = ddsPublisher;
        _logger = logger;
        _ddsSubscriber = ddsSubscriber;
        _transactionEventRepository = transactionEventRepository;
    }

    public Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        if (_ddsSubscriber != null)
        {
            _listenerTask = ListenForCommandsViaRealDdsAsync(_cts.Token);
            _logger.LogInformation("Listening for payment commands using real CycloneDDS topics...");
        }
        else
        {
            _listenerTask = ListenForCommandsInMemoryAsync(_cts.Token);
            _logger.LogInformation("Listening for payment commands in in-memory mode...");
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_listenerTask != null)
            await _listenerTask;
    }

    private async Task ListenForCommandsViaRealDdsAsync(CancellationToken ct)
    {
        if (_ddsSubscriber == null)
            return;

        await _ddsSubscriber.SubscribeAsync<PaymentCreateCommand>("payment.create",
            message => ProcessPaymentCreateAsync(message, ct), ct);

        await _ddsSubscriber.SubscribeAsync<PaymentCaptureCommand>("payment.capture",
            message => ProcessPaymentCaptureAsync(message, ct), ct);

        await _ddsSubscriber.SubscribeAsync<PaymentRefundCommand>("payment.refund",
            message => ProcessPaymentRefundAsync(message, ct), ct);

        await _ddsSubscriber.SubscribeAsync<PaymentRejectCommand>("payment.reject",
            message => ProcessPaymentRejectAsync(message, ct), ct);

        await _ddsSubscriber.SubscribeAsync<PaymentCancelCommand>("payment.cancel",
            message => ProcessPaymentCancelAsync(message, ct), ct);

        await _ddsSubscriber.SubscribeAsync<FraudCheckedEvent>("fraud.checked",
            message => ProcessFraudCheckedAsync(message, ct), ct);

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    private async Task ListenForCommandsInMemoryAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_ddsPublisher is InMemoryDdsPublisher inMemory)
                {
                    var createdMessages = inMemory.DequeueMessages("payment.create");
                    foreach (var msg in createdMessages)
                    {
                        await ProcessPaymentCreateAsync(msg, ct);
                    }

                    var captureMessages = inMemory.DequeueMessages("payment.capture");
                    foreach (var msg in captureMessages)
                    {
                        await ProcessPaymentCaptureAsync(msg, ct);
                    }

                    var refundMessages = inMemory.DequeueMessages("payment.refund");
                    foreach (var msg in refundMessages)
                    {
                        await ProcessPaymentRefundAsync(msg, ct);
                    }

                    var rejectMessages = inMemory.DequeueMessages("payment.reject");
                    foreach (var msg in rejectMessages)
                    {
                        await ProcessPaymentRejectAsync(msg, ct);
                    }

                    var cancelMessages = inMemory.DequeueMessages("payment.cancel");
                    foreach (var msg in cancelMessages)
                    {
                        await ProcessPaymentCancelAsync(msg, ct);
                    }

                    var fraudCheckedMessages = inMemory.DequeueMessages("fraud.checked");
                    foreach (var msg in fraudCheckedMessages)
                    {
                        await ProcessFraudCheckedAsync(msg, ct);
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
                _logger.LogError(ex, "Error in payment command listener");
                await Task.Delay(1000, ct);
            }
        }
    }

    private Task ProcessPaymentCreateAsync(PaymentCreateCommand message, CancellationToken ct)
    {
        _logger.LogInformation("Processing payment.create from DDS for payment {PaymentId}", message.PaymentId);
        return ProcessPaymentCreateCoreAsync(message.PaymentId, ct);
    }

    private async Task ProcessPaymentCreateAsync(object message, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "Processing payment.create payload {@Payload}",
                SensitiveDataSanitizer.Sanitize(message));

            var command = JsonSerializer.Deserialize<PaymentCreateCommand>(
                JsonSerializer.Serialize(message),
                JsonOptions);

            if (command.PaymentId == Guid.Empty)
            {
                _logger.LogWarning("Ignoring payment.create with invalid PaymentId");
                return;
            }

            await ProcessPaymentCreateCoreAsync(command.PaymentId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment.create");
        }
    }

    private async Task ProcessPaymentCreateCoreAsync(Guid paymentId, CancellationToken ct)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "payment",
            "process_create",
            ActivityKind.Consumer,
            ("payment.id", paymentId.ToString()));

        if (paymentId == Guid.Empty)
        {
            operation.SetResult("ignored");
            _logger.LogWarning("Ignoring payment.create with empty PaymentId");
            return;
        }

        try
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId, ct);
            if (payment == null)
            {
                operation.SetResult("not_found");
                _logger.LogWarning("Payment {PaymentId} not found", paymentId);
                return;
            }

            var now = DateTime.UtcNow;
            await _ddsPublisher.PublishAsync("payment.created", new
            {
                paymentId = payment.Id,
                merchantId = payment.MerchantId,
                amount = payment.Amount.Amount,
                currency = payment.Amount.Currency,
                status = payment.Status.ToString().ToLowerInvariant(),
                createdAt = payment.CreatedAt
            }, ct);

            await _ddsPublisher.PublishAsync("fraud.check", new
            {
                paymentId = payment.Id,
                merchantId = payment.MerchantId,
                amount = payment.Amount.Amount,
                currency = payment.Amount.Currency,
                customer = new
                {
                    email = payment.Customer.Email,
                    document = payment.Customer.Document,
                    ip = payment.Customer.Ip,
                    phone = payment.Customer.Phone
                },
                timestamp = now
            }, ct);

            _logger.LogInformation("Payment {PaymentId} published to payment.created and fraud.check", paymentId);
        }
        catch (Exception ex)
        {
            operation.RecordException(ex);
            throw;
        }
    }

    private async Task ProcessFraudCheckedAsync(object message, CancellationToken ct)
    {
        try
        {
            var fraudEvent = JsonSerializer.Deserialize<FraudCheckedEvent>(
                JsonSerializer.Serialize(message),
                JsonOptions);

            if (fraudEvent.PaymentId == Guid.Empty)
            {
                _logger.LogWarning("Ignoring fraud.checked with invalid PaymentId");
                return;
            }

            await ProcessFraudCheckedAsync(fraudEvent, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing fraud.checked message");
        }
    }

    private async Task ProcessFraudCheckedAsync(FraudCheckedEvent fraudEvent, CancellationToken ct)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "payment",
            "apply_fraud_result",
            ActivityKind.Consumer,
            ("payment.id", fraudEvent.PaymentId.ToString()),
            ("fraud.decision", fraudEvent.Decision));

        try
        {
            var payment = await _paymentRepository.GetByIdAsync(fraudEvent.PaymentId, ct);
            if (payment == null)
            {
                operation.SetResult("not_found");
                _logger.LogWarning("Payment {PaymentId} not found for fraud.checked", fraudEvent.PaymentId);
                return;
            }

            var reasons = string.IsNullOrWhiteSpace(fraudEvent.Reasons)
                ? Array.Empty<string>()
                : fraudEvent.Reasons
                    .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var decision = ParseFraudDecision(fraudEvent.Decision);
            var result = new FraudCheckResult((decimal)fraudEvent.RiskScore, decision, reasons);
            payment.ApplyFraudResult(result);

            if (decision == FraudDecision.Approved)
            {
                payment.Approve(payment.TransactionId);

                // Store authorization transaction event
                if (_transactionEventRepository != null)
                {
                    await _transactionEventRepository.StoreAsync(new StoredTransactionEvent
                    {
                        Id = Guid.NewGuid(),
                        PaymentId = payment.Id,
                        MerchantId = payment.MerchantId,
                        EventType = "authorization",
                        PayloadJson = JsonSerializer.Serialize(new
                        {
                            transactionId = payment.TransactionId,
                            amount = payment.Amount.Amount,
                            currency = payment.Amount.Currency,
                            fraudScore = fraudEvent.RiskScore,
                            fraudDecision = fraudEvent.Decision,
                            method = payment.Method.ToString()
                        }),
                        OccurredAt = DateTime.UtcNow
                    }, ct);
                }

                await _ddsPublisher.PublishAsync("payment.approved", new
                {
                    paymentId = payment.Id,
                    merchantId = payment.MerchantId,
                    amount = payment.Amount.Amount,
                    currency = payment.Amount.Currency,
                    transactionId = payment.TransactionId,
                    processedAt = payment.ProcessedAt ?? DateTime.UtcNow
                }, ct);

                _logger.LogInformation("Transaction {TransactionId} created for approved payment {PaymentId}",
                    payment.TransactionId, payment.Id);
            }
            else if (decision == FraudDecision.Rejected)
            {
                payment.Reject("Fraud rejected: " + string.Join("; ", reasons));

                await _ddsPublisher.PublishAsync("payment.rejected", new
                {
                    paymentId = payment.Id,
                    merchantId = payment.MerchantId,
                    reason = reasons.Length > 0 ? string.Join("; ", reasons) : "Fraud rejected",
                    processedAt = payment.ProcessedAt ?? DateTime.UtcNow
                }, ct);
            }

            await _paymentRepository.UpdateAsync(payment, ct);
            PaymentGatewayTelemetry.RecordPaymentLifecycle(
                "fraud_result_applied",
                decision.ToString().ToLowerInvariant(),
                payment.Method.ToString(),
                payment.MerchantId);
            _logger.LogInformation(
                "Fraud decision applied to payment {PaymentId}: {Decision} ({RiskScore})",
                payment.Id,
                decision,
                fraudEvent.RiskScore);
        }
        catch (Exception ex)
        {
            operation.RecordException(ex);
            throw;
        }
    }

    private static FraudDecision ParseFraudDecision(string? decision)
    {
        return Enum.TryParse<FraudDecision>(decision, true, out var parsed)
            ? parsed
            : FraudDecision.Review;
    }

    private Task ProcessPaymentCaptureAsync(PaymentCaptureCommand message, CancellationToken ct)
    {
        _logger.LogInformation("Processing payment.capture from DDS for payment {PaymentId}", message.PaymentId);
        return ProcessPaymentCaptureCoreAsync(message.PaymentId, (decimal)message.Amount, ct);
    }

    private async Task ProcessPaymentCaptureAsync(object message, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "Processing payment.capture payload {@Payload}",
                SensitiveDataSanitizer.Sanitize(message));

            var paymentData = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(message));
            if (paymentData == null) return;

            var paymentId = Guid.Parse(paymentData["paymentId"]?.ToString() ?? Guid.Empty.ToString());
            var amount = decimal.TryParse(paymentData["amount"]?.ToString(), out var parsedAmount)
                ? parsedAmount
                : (decimal?)null;

            await ProcessPaymentCaptureCoreAsync(paymentId, amount, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment.capture");
        }
    }

    private async Task ProcessPaymentCaptureCoreAsync(Guid paymentId, decimal? amount, CancellationToken ct)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "payment",
            "capture",
            ActivityKind.Consumer,
            ("payment.id", paymentId.ToString()));

        try
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId, ct);
            if (payment == null)
            {
                operation.SetResult("not_found");
                _logger.LogWarning("Payment {PaymentId} not found", paymentId);
                return;
            }

            payment.Capture(amount);
            await _paymentRepository.UpdateAsync(payment, ct);

            await _ddsPublisher.PublishAsync("payment.captured", new
            {
                paymentId = payment.Id,
                merchantId = payment.MerchantId,
                amount = payment.Amount.Amount,
                currency = payment.Amount.Currency,
                capturedAt = payment.CapturedAt
            }, ct);

            PaymentGatewayTelemetry.RecordPaymentLifecycle(
                "captured",
                payment.Status.ToString().ToLowerInvariant(),
                payment.Method.ToString(),
                payment.MerchantId);
            _logger.LogInformation("Payment {PaymentId} captured", paymentId);
        }
        catch (Exception ex)
        {
            operation.RecordException(ex);
            throw;
        }
    }

    private Task ProcessPaymentRefundAsync(PaymentRefundCommand message, CancellationToken ct)
    {
        _logger.LogInformation("Processing payment.refund from DDS for payment {PaymentId}", message.PaymentId);
        return ProcessPaymentRefundCoreAsync(
            message.PaymentId,
            (decimal)message.Amount,
            message.Reason,
            ct);
    }

    private async Task ProcessPaymentRefundAsync(object message, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "Processing payment.refund payload {@Payload}",
                SensitiveDataSanitizer.Sanitize(message));

            var paymentData = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(message));
            if (paymentData == null) return;

            var paymentId = Guid.Parse(paymentData["paymentId"]?.ToString() ?? Guid.Empty.ToString());
            var amount = decimal.TryParse(paymentData["amount"]?.ToString(), out var a) ? a : (decimal?)null;
            var reason = paymentData["reason"]?.ToString() ?? string.Empty;

            await ProcessPaymentRefundCoreAsync(paymentId, amount, reason, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment.refund");
        }
    }

    private async Task ProcessPaymentRefundCoreAsync(Guid paymentId, decimal? amount, string? reason, CancellationToken ct)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "payment",
            "refund",
            ActivityKind.Consumer,
            ("payment.id", paymentId.ToString()));

        try
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId, ct);
            if (payment == null)
            {
                operation.SetResult("not_found");
                _logger.LogWarning("Payment {PaymentId} not found", paymentId);
                return;
            }

            payment.Refund(amount);
            await _paymentRepository.UpdateAsync(payment, ct);

            await _ddsPublisher.PublishAsync("payment.refunded", new
            {
                paymentId = payment.Id,
                merchantId = payment.MerchantId,
                amount = payment.RefundedAmount?.Amount ?? amount ?? payment.Amount.Amount,
                currency = payment.Amount.Currency,
                reason = reason,
                refundedAt = payment.RefundedAt
            }, ct);

            PaymentGatewayTelemetry.RecordPaymentLifecycle(
                "refunded",
                payment.Status.ToString().ToLowerInvariant(),
                payment.Method.ToString(),
                payment.MerchantId);
            _logger.LogInformation("Payment {PaymentId} refunded", paymentId);
        }
        catch (Exception ex)
        {
            operation.RecordException(ex);
            throw;
        }
    }

    private Task ProcessPaymentRejectAsync(PaymentRejectCommand message, CancellationToken ct)
    {
        _logger.LogInformation("Processing payment.reject from DDS for payment {PaymentId}", message.PaymentId);
        return ProcessPaymentRejectCoreAsync(message.PaymentId, message.Reason, ct);
    }

    private async Task ProcessPaymentRejectAsync(object message, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "Processing payment.reject payload {@Payload}",
                SensitiveDataSanitizer.Sanitize(message));

            var paymentData = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(message));
            if (paymentData == null) return;

            var paymentId = Guid.Parse(paymentData["paymentId"]?.ToString() ?? Guid.Empty.ToString());
            var reason = paymentData["reason"]?.ToString() ?? "Payment rejected";

            await ProcessPaymentRejectCoreAsync(paymentId, reason, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment.reject");
        }
    }

    private async Task ProcessPaymentRejectCoreAsync(Guid paymentId, string reason, CancellationToken ct)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "payment",
            "reject",
            ActivityKind.Consumer,
            ("payment.id", paymentId.ToString()));

        try
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId, ct);
            if (payment == null)
            {
                operation.SetResult("not_found");
                return;
            }

            payment.Reject(reason);
            await _paymentRepository.UpdateAsync(payment, ct);

            await _ddsPublisher.PublishAsync("payment.rejected", new
            {
                paymentId = payment.Id,
                merchantId = payment.MerchantId,
                reason = reason,
                rejectedAt = payment.ProcessedAt
            }, ct);

            PaymentGatewayTelemetry.RecordPaymentLifecycle(
                "rejected",
                payment.Status.ToString().ToLowerInvariant(),
                payment.Method.ToString(),
                payment.MerchantId);
            _logger.LogInformation("Payment {PaymentId} rejected: {Reason}", paymentId, reason);
        }
        catch (Exception ex)
        {
            operation.RecordException(ex);
            throw;
        }
    }

    private Task ProcessPaymentCancelAsync(PaymentCancelCommand message, CancellationToken ct)
    {
        _logger.LogInformation("Processing payment.cancel from DDS for payment {PaymentId}", message.PaymentId);
        return ProcessPaymentCancelCoreAsync(message.PaymentId, message.Reason, ct);
    }

    private async Task ProcessPaymentCancelAsync(object message, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "Processing payment.cancel payload {@Payload}",
                SensitiveDataSanitizer.Sanitize(message));

            var paymentData = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(message));
            if (paymentData == null) return;

            var paymentId = Guid.Parse(paymentData["paymentId"]?.ToString() ?? Guid.Empty.ToString());
            var reason = paymentData["reason"]?.ToString() ?? string.Empty;

            await ProcessPaymentCancelCoreAsync(paymentId, reason, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment.cancel");
        }
    }

    private async Task ProcessPaymentCancelCoreAsync(Guid paymentId, string? reason, CancellationToken ct)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "payment",
            "cancel",
            ActivityKind.Consumer,
            ("payment.id", paymentId.ToString()));

        try
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId, ct);
            if (payment == null)
            {
                operation.SetResult("not_found");
                return;
            }

            payment.Cancel();
            await _paymentRepository.UpdateAsync(payment, ct);

            await _ddsPublisher.PublishAsync("payment.cancelled", new
            {
                paymentId = payment.Id,
                merchantId = payment.MerchantId,
                reason = reason,
                cancelledAt = payment.CancelledAt
            }, ct);

            PaymentGatewayTelemetry.RecordPaymentLifecycle(
                "cancelled",
                payment.Status.ToString().ToLowerInvariant(),
                payment.Method.ToString(),
                payment.MerchantId);
            _logger.LogInformation("Payment {PaymentId} cancelled", paymentId);
        }
        catch (Exception ex)
        {
            operation.RecordException(ex);
            throw;
        }
    }
}
