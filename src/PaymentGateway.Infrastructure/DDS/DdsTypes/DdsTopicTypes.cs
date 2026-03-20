using CycloneDDS.Schema;

namespace PaymentGateway.Infrastructure.DDS.DdsTypes;

/// <summary>
/// DDS topic types for PaymentGateway.
/// These types are used for CycloneDDS pub/sub when code generation is enabled.
/// </summary>
[DdsTopic("payment.create")]
[DdsManaged]
public partial struct PaymentCreateCommand
{
    [DdsId(0), DdsKey]
    public Guid PaymentId;
    [DdsId(1)]
    public Guid MerchantId;
    [DdsId(2)]
    public double Amount;
    [DdsId(3)]
    public string Currency;
    [DdsId(4)]
    public string Method;
    [DdsId(5)]
    public string CustomerEmail;
    [DdsId(6)]
    public string CustomerName;
    [DdsId(7)]
    public string CustomerDocument;
    [DdsId(8)]
    public string CustomerIp;
    [DdsId(9)]
    public string CustomerPhone;
    [DdsId(10)]
    public DateTime Timestamp;
}

[DdsTopic("payment.created")]
[DdsManaged]
public partial struct PaymentCreatedEvent
{
    [DdsId(0), DdsKey]
    public Guid PaymentId;
    [DdsId(1)]
    public Guid MerchantId;
    [DdsId(2)]
    public double Amount;
    [DdsId(3)]
    public string Currency;
    [DdsId(4)]
    public string Status;
    [DdsId(5)]
    public DateTime CreatedAt;
}

[DdsTopic("payment.capture")]
[DdsManaged]
public partial struct PaymentCaptureCommand
{
    [DdsId(0), DdsKey]
    public Guid PaymentId;
    [DdsId(1)]
    public Guid MerchantId;
    [DdsId(2)]
    public double Amount;
    [DdsId(3)]
    public DateTime Timestamp;
}

[DdsTopic("payment.refund")]
[DdsManaged]
public partial struct PaymentRefundCommand
{
    [DdsId(0), DdsKey]
    public Guid PaymentId;
    [DdsId(1)]
    public Guid MerchantId;
    [DdsId(2)]
    public double Amount;
    [DdsId(3)]
    public string Reason;
    [DdsId(4)]
    public DateTime Timestamp;
}

[DdsTopic("payment.reject")]
[DdsManaged]
public partial struct PaymentRejectCommand
{
    [DdsId(0), DdsKey]
    public Guid PaymentId;
    [DdsId(1)]
    public Guid MerchantId;
    [DdsId(2)]
    public string Reason;
    [DdsId(3)]
    public DateTime Timestamp;
}

[DdsTopic("payment.cancel")]
[DdsManaged]
public partial struct PaymentCancelCommand
{
    [DdsId(0), DdsKey]
    public Guid PaymentId;
    [DdsId(1)]
    public Guid MerchantId;
    [DdsId(2)]
    public string Reason;
    [DdsId(3)]
    public DateTime Timestamp;
}

[DdsTopic("payment.approved")]
[DdsManaged]
public partial struct PaymentApprovedEvent
{
    [DdsId(0), DdsKey]
    public Guid PaymentId;
    [DdsId(1)]
    public Guid MerchantId;
    [DdsId(2)]
    public string TransactionId;
    [DdsId(3)]
    public double Amount;
    [DdsId(4)]
    public string Currency;
    [DdsId(5)]
    public DateTime ProcessedAt;
}

[DdsTopic("payment.rejected")]
[DdsManaged]
public partial struct PaymentRejectedEvent
{
    [DdsId(0), DdsKey]
    public Guid PaymentId;
    [DdsId(1)]
    public Guid MerchantId;
    [DdsId(2)]
    public string Reason;
    [DdsId(3)]
    public DateTime ProcessedAt;
}

[DdsTopic("payment.captured")]
[DdsManaged]
public partial struct PaymentCapturedEvent
{
    [DdsId(0), DdsKey]
    public Guid PaymentId;
    [DdsId(1)]
    public Guid MerchantId;
    [DdsId(2)]
    public double Amount;
    [DdsId(3)]
    public string Currency;
    [DdsId(4)]
    public DateTime CapturedAt;
}

[DdsTopic("payment.refunded")]
[DdsManaged]
public partial struct PaymentRefundedEvent
{
    [DdsId(0), DdsKey]
    public Guid PaymentId;
    [DdsId(1)]
    public Guid MerchantId;
    [DdsId(2)]
    public double Amount;
    [DdsId(3)]
    public string Currency;
    [DdsId(4)]
    public string Reason;
    [DdsId(5)]
    public DateTime RefundedAt;
}

[DdsTopic("payment.cancelled")]
[DdsManaged]
public partial struct PaymentCancelledEvent
{
    [DdsId(0), DdsKey]
    public Guid PaymentId;
    [DdsId(1)]
    public Guid MerchantId;
    [DdsId(2)]
    public string Reason;
    [DdsId(3)]
    public DateTime CancelledAt;
}

[DdsTopic("fraud.check")]
[DdsManaged]
public partial struct FraudCheckCommand
{
    [DdsId(0), DdsKey]
    public Guid PaymentId;
    [DdsId(1)]
    public Guid MerchantId;
    [DdsId(2)]
    public double Amount;
    [DdsId(3)]
    public string Currency;
    [DdsId(4)]
    public string CustomerEmail;
    [DdsId(5)]
    public string CustomerDocument;
    [DdsId(6)]
    public string CustomerIp;
    [DdsId(7)]
    public DateTime Timestamp;
}

[DdsTopic("fraud.checked")]
[DdsManaged]
public partial struct FraudCheckedEvent
{
    [DdsId(0), DdsKey]
    public Guid PaymentId;
    [DdsId(1)]
    public double RiskScore;
    [DdsId(2)]
    public string Decision;
    [DdsId(3)]
    public string Reasons;
    [DdsId(4)]
    public DateTime Timestamp;
}

[DdsTopic("settlement.processed")]
[DdsManaged]
public partial struct SettlementProcessedEvent
{
    [DdsId(0), DdsKey]
    public Guid SettlementId;
    [DdsId(1)]
    public Guid MerchantId;
    [DdsId(2)]
    public DateTime PeriodStart;
    [DdsId(3)]
    public DateTime PeriodEnd;
    [DdsId(4)]
    public double TotalAmount;
    [DdsId(5)]
    public double FeeAmount;
    [DdsId(6)]
    public double SettlementAmount;
    [DdsId(7)]
    public string Status;
    [DdsId(8)]
    public DateTime ProcessedAt;
}
