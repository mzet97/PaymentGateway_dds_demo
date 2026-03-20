namespace PaymentGateway.Domain.Entities;

public enum TransactionType
{
    Authorization = 0,
    Capture = 1,
    Refund = 2,
    Cancellation = 3
}
