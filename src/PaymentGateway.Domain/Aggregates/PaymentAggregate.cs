using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.Events;
using PaymentGateway.Domain.ValueObjects;

namespace PaymentGateway.Domain.Aggregates;

public class PaymentAggregate : AggregateRoot
{
    public Guid MerchantId { get; private set; }
    public Money Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public PaymentMethod Method { get; private set; }
    public CustomerInfo Customer { get; private set; }
    public FraudCheckResult? FraudResult { get; private set; }
    public string? Description { get; private set; }
    public IReadOnlyList<PaymentItem> Items { get; private set; } = Array.Empty<PaymentItem>();
    public IReadOnlyDictionary<string, string> Metadata { get; private set; } = new Dictionary<string, string>();
    public DateTime? ProcessedAt { get; private set; }
    public DateTime? CapturedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime? RefundedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public string? TransactionId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public Money? RefundedAmount { get; private set; }

    private PaymentAggregate() : base()
    {
        Amount = Money.Zero;
        Customer = new CustomerInfo("", "", "");
    }

    public static PaymentAggregate Create(
        Guid merchantId,
        Money amount,
        PaymentMethod method,
        CustomerInfo customer,
        string? description = null,
        IEnumerable<PaymentItem>? items = null,
        IDictionary<string, string>? metadata = null,
        string? idempotencyKey = null)
    {
        var aggregate = new PaymentAggregate
        {
            MerchantId = merchantId,
            Amount = amount,
            Status = PaymentStatus.Pending,
            Method = method,
            Customer = customer,
            Description = description,
            Items = items?.ToList().AsReadOnly() ?? new List<PaymentItem>().AsReadOnly(),
            Metadata = metadata?.ToDictionary(kv => kv.Key, kv => kv.Value).AsReadOnly() ?? new Dictionary<string, string>().AsReadOnly(),
            IdempotencyKey = idempotencyKey,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        aggregate.AddDomainEvent(new PaymentCreatedEvent
        {
            PaymentId = aggregate.Id,
            MerchantId = merchantId,
            Amount = amount.Amount,
            Currency = amount.Currency,
            Method = method.ToString(),
            CustomerEmail = customer.Email,
            IdempotencyKey = idempotencyKey
        });

        return aggregate;
    }

    public void ApplyFraudResult(FraudCheckResult result)
    {
        FraudResult = result;

        Status = result.Decision switch
        {
            Enums.FraudDecision.Approved => PaymentStatus.Authorized,
            Enums.FraudDecision.Review => PaymentStatus.Pending,
            Enums.FraudDecision.Rejected => PaymentStatus.Rejected,
            _ => Status
        };

        ProcessedAt = DateTime.UtcNow;

        AddDomainEvent(new FraudCheckedEvent
        {
            PaymentId = Id,
            RiskScore = result.RiskScore,
            Decision = result.Decision.ToString(),
            Reasons = result.Reasons,
            Model = result.Metadata?.Model,
            LatencyMs = result.Metadata?.LatencyMs ?? 0
        });
    }

    public void Approve(string? transactionId = null)
    {
        EnsureCanApprove();

        Status = PaymentStatus.Approved;
        ProcessedAt = DateTime.UtcNow;
        TransactionId = transactionId ?? Guid.NewGuid().ToString();

        AddDomainEvent(new PaymentApprovedEvent
        {
            PaymentId = Id,
            MerchantId = MerchantId,
            Amount = Amount.Amount,
            Currency = Amount.Currency,
            TransactionId = TransactionId,
            FraudScore = FraudResult?.RiskScore ?? 50
        });
    }

    public void Reject(string reason)
    {
        EnsureCanReject();

        Status = PaymentStatus.Rejected;
        ProcessedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentRejectedEvent
        {
            PaymentId = Id,
            MerchantId = MerchantId,
            Reason = reason,
            FraudScore = FraudResult?.RiskScore ?? 50
        });
    }

    public void Capture(decimal? amount = null)
    {
        EnsureCanCapture();

        var captureAmount = amount.HasValue ? new Money(amount.Value, Amount.Currency) : Amount;

        if (captureAmount > Amount)
            throw new InvalidOperationException("Capture amount cannot exceed payment amount");

        if (captureAmount < Amount)
        {
            Status = PaymentStatus.PartiallyRefunded;
            RefundedAmount = new Money(Amount.Amount - captureAmount.Amount, Amount.Currency);
        }
        else
        {
            Status = PaymentStatus.Captured;
        }

        CapturedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentCapturedEvent
        {
            PaymentId = Id,
            Amount = captureAmount,
            Currency = Amount.Currency
        });
    }

    public void Cancel(string? reason = null)
    {
        EnsureCanCancel();

        Status = PaymentStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentCancelledEvent
        {
            PaymentId = Id,
            Reason = reason
        });
    }

    public void Refund(decimal? amount = null, string? reason = null)
    {
        EnsureCanRefund();

        var refundAmount = amount.HasValue ? new Money(amount.Value, Amount.Currency) : Amount;

        var currentRefunded = RefundedAmount ?? Money.Zero;
        var newTotalRefunded = currentRefunded.Amount + refundAmount.Amount;

        if (newTotalRefunded >= Amount.Amount)
        {
            Status = PaymentStatus.Refunded;
            RefundedAmount = Amount;
        }
        else
        {
            Status = PaymentStatus.PartiallyRefunded;
            RefundedAmount = new Money(newTotalRefunded, Amount.Currency);
        }

        RefundedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentRefundedEvent
        {
            PaymentId = Id,
            RefundId = Guid.NewGuid(),
            Amount = refundAmount,
            Currency = Amount.Currency,
            Reason = reason
        });
    }

    public void Expire()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException("Only pending payments can expire");

        Status = PaymentStatus.Expired;

        AddDomainEvent(new PaymentExpiredEvent { PaymentId = Id });
    }

    public bool IsExpired => Status == PaymentStatus.Pending && DateTime.UtcNow > ExpiresAt;

    public bool CanTransitionTo(PaymentStatus newStatus)
    {
        return (Status, newStatus) switch
        {
            (PaymentStatus.Pending, PaymentStatus.Authorized) => true,
            (PaymentStatus.Pending, PaymentStatus.Approved) => true,
            (PaymentStatus.Pending, PaymentStatus.Rejected) => true,
            (PaymentStatus.Pending, PaymentStatus.Cancelled) => true,
            (PaymentStatus.Pending, PaymentStatus.Expired) => true,
            (PaymentStatus.Authorized, PaymentStatus.Approved) => true,
            (PaymentStatus.Authorized, PaymentStatus.Captured) => true,
            (PaymentStatus.Authorized, PaymentStatus.Cancelled) => true,
            (PaymentStatus.Approved, PaymentStatus.Captured) => true,
            (PaymentStatus.Approved, PaymentStatus.Refunded) => true,
            (PaymentStatus.Approved, PaymentStatus.PartiallyRefunded) => true,
            (PaymentStatus.Captured, PaymentStatus.Refunded) => true,
            (PaymentStatus.Captured, PaymentStatus.PartiallyRefunded) => true,
            (PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded) => true,
            _ => false
        };
    }

    private void EnsureCanApprove()
    {
        if (!CanTransitionTo(PaymentStatus.Approved))
            throw new InvalidOperationException($"Cannot approve payment with status {Status}");
    }

    private void EnsureCanReject()
    {
        if (!CanTransitionTo(PaymentStatus.Rejected))
            throw new InvalidOperationException($"Cannot reject payment with status {Status}");
    }

    private void EnsureCanCapture()
    {
        if (!CanTransitionTo(PaymentStatus.Captured))
            throw new InvalidOperationException($"Cannot capture payment with status {Status}");
    }

    private void EnsureCanCancel()
    {
        if (!CanTransitionTo(PaymentStatus.Cancelled))
            throw new InvalidOperationException($"Cannot cancel payment with status {Status}");
    }

    private void EnsureCanRefund()
    {
        if (!CanTransitionTo(PaymentStatus.Refunded) && !CanTransitionTo(PaymentStatus.PartiallyRefunded))
            throw new InvalidOperationException($"Cannot refund payment with status {Status}");
    }
}
