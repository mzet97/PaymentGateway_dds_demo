using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.ValueObjects;

namespace PaymentGateway.Domain.Entities;

public class Transaction : Entity
{
    public Guid PaymentId { get; private set; }
    public TransactionType Type { get; private set; }
    public Money Amount { get; private set; }
    public string? Reference { get; private set; }
    public string? Reason { get; private set; }
    public bool Success { get; private set; }
    public string? ErrorCode { get; private set; }

    private Transaction() : base()
    {
        Amount = Money.Zero;
    }

    public static Transaction CreateAuthorization(
        Guid paymentId,
        Money amount,
        string? reference = null)
    {
        return new Transaction
        {
            PaymentId = paymentId,
            Type = TransactionType.Authorization,
            Amount = amount,
            Reference = reference ?? Guid.NewGuid().ToString(),
            Success = true
        };
    }

    public static Transaction CreateCapture(
        Guid paymentId,
        Money amount,
        string? reference = null)
    {
        return new Transaction
        {
            PaymentId = paymentId,
            Type = TransactionType.Capture,
            Amount = amount,
            Reference = reference ?? Guid.NewGuid().ToString(),
            Success = true
        };
    }

    public static Transaction CreateRefund(
        Guid paymentId,
        Money amount,
        string? reference = null,
        string? reason = null)
    {
        return new Transaction
        {
            PaymentId = paymentId,
            Type = TransactionType.Refund,
            Amount = amount,
            Reference = reference ?? Guid.NewGuid().ToString(),
            Reason = reason,
            Success = true
        };
    }

    public static Transaction CreateCancellation(
        Guid paymentId,
        string? reason = null)
    {
        return new Transaction
        {
            PaymentId = paymentId,
            Type = TransactionType.Cancellation,
            Amount = Money.Zero,
            Reason = reason,
            Success = true
        };
    }

    public static Transaction CreateFailed(
        Guid paymentId,
        TransactionType type,
        Money amount,
        string errorCode,
        string? reason = null)
    {
        return new Transaction
        {
            PaymentId = paymentId,
            Type = type,
            Amount = amount,
            Success = false,
            ErrorCode = errorCode,
            Reason = reason
        };
    }
}
