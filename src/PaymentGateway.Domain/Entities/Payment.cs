using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.ValueObjects;

namespace PaymentGateway.Domain.Entities;

public class Payment : Entity
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

    private Payment() : base()
    {
        Amount = Money.Zero;
        Customer = new CustomerInfo("placeholder@example.com", "placeholder", "00000000000");
    }

    public static Payment Create(
        Guid merchantId,
        Money amount,
        PaymentMethod method,
        CustomerInfo customer,
        string? description = null,
        IEnumerable<PaymentItem>? items = null,
        IDictionary<string, string>? metadata = null,
        string? idempotencyKey = null)
    {
        var payment = new Payment
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

        return payment;
    }

    public static Payment Rehydrate(
        Guid id,
        Guid merchantId,
        Money amount,
        PaymentStatus status,
        PaymentMethod method,
        CustomerInfo customer,
        string? description,
        IEnumerable<PaymentItem>? items,
        IDictionary<string, string>? metadata,
        FraudCheckResult? fraudResult,
        string? transactionId,
        string? idempotencyKey,
        DateTime createdAt,
        DateTime? updatedAt,
        DateTime? processedAt,
        DateTime expiresAt,
        DateTime? capturedAt,
        DateTime? cancelledAt,
        DateTime? refundedAt,
        Money? refundedAmount)
    {
        return new Payment
        {
            Id = id,
            MerchantId = merchantId,
            Amount = amount,
            Status = status,
            Method = method,
            Customer = customer,
            Description = description,
            Items = items?.ToList().AsReadOnly() ?? new List<PaymentItem>().AsReadOnly(),
            Metadata = metadata?.ToDictionary(kv => kv.Key, kv => kv.Value).AsReadOnly() ?? new Dictionary<string, string>().AsReadOnly(),
            FraudResult = fraudResult,
            TransactionId = transactionId,
            IdempotencyKey = idempotencyKey,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            ProcessedAt = processedAt,
            ExpiresAt = expiresAt,
            CapturedAt = capturedAt,
            CancelledAt = cancelledAt,
            RefundedAt = refundedAt,
            RefundedAmount = refundedAmount
        };
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
    }

    public void Approve(string? transactionId = null)
    {
        EnsureCanApprove();

        Status = PaymentStatus.Approved;
        ProcessedAt = DateTime.UtcNow;
        TransactionId = transactionId ?? Guid.NewGuid().ToString();
    }

    public void Reject(string reason)
    {
        EnsureCanReject();

        Status = PaymentStatus.Rejected;
        ProcessedAt = DateTime.UtcNow;
    }

    public void Capture(decimal? amount = null)
    {
        EnsureCanCapture();

        var captureAmount = amount.HasValue ? new Money(amount.Value, Amount.Currency) : Amount;

        if (captureAmount > Amount)
            throw new InvalidOperationException("Capture amount cannot exceed payment amount");

        if (captureAmount < Amount)
            throw new InvalidOperationException("Partial capture is not supported");

        Status = PaymentStatus.Captured;

        CapturedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        EnsureCanCancel();

        Status = PaymentStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
    }

    public void Refund(decimal? amount = null)
    {
        EnsureCanRefund();

        var refundAmount = amount.HasValue ? new Money(amount.Value, Amount.Currency) : Amount;

        if (refundAmount > Amount)
            throw new InvalidOperationException("Refund amount cannot exceed payment amount");

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
    }

    public void Expire()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException("Only pending payments can expire");

        Status = PaymentStatus.Expired;
    }

    public bool IsExpired => Status == PaymentStatus.Pending && DateTime.UtcNow > ExpiresAt;

    private void EnsureCanApprove()
    {
        if (Status != PaymentStatus.Pending && Status != PaymentStatus.Authorized)
            throw new InvalidOperationException($"Cannot approve payment with status {Status}");
    }

    private void EnsureCanReject()
    {
        if (Status == PaymentStatus.Approved || Status == PaymentStatus.Captured)
            throw new InvalidOperationException($"Cannot reject payment with status {Status}");

        if (Status == PaymentStatus.Rejected || Status == PaymentStatus.Cancelled)
            throw new InvalidOperationException("Payment is already rejected or cancelled");
    }

    private void EnsureCanCapture()
    {
        if (Status != PaymentStatus.Authorized && Status != PaymentStatus.Approved)
            throw new InvalidOperationException($"Cannot capture payment with status {Status}");
    }

    private void EnsureCanCancel()
    {
        if (Status != PaymentStatus.Pending && Status != PaymentStatus.Authorized)
            throw new InvalidOperationException($"Cannot cancel payment with status {Status}");
    }

    private void EnsureCanRefund()
    {
        if (Status != PaymentStatus.Captured && Status != PaymentStatus.Approved)
            throw new InvalidOperationException($"Cannot refund payment with status {Status}");

        if (Status == PaymentStatus.Refunded)
            throw new InvalidOperationException("Payment is already fully refunded");
    }
}
