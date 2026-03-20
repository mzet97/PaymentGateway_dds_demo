namespace PaymentGateway.Domain.Enums;

public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,
    Captured = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5,
    Refunded = 6,
    PartiallyRefunded = 7,
    Expired = 8
}
