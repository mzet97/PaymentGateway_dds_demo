namespace PaymentGateway.Domain.Entities;

public enum MerchantStatus
{
    PendingVerification = 0,
    Active = 1,
    Suspended = 2,
    Inactive = 3
}
