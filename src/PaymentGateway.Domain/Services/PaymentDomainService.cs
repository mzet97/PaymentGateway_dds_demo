using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.ValueObjects;

namespace PaymentGateway.Domain.Services;

public class PaymentDomainService
{
    private const decimal PlatformFeePercentage = 0.029m;
    private const decimal PlatformFeeFixed = 0.30m;

    public decimal CalculatePlatformFee(decimal amount)
    {
        return Math.Round(amount * PlatformFeePercentage + PlatformFeeFixed, 2);
    }

    public decimal CalculateSettlementAmount(decimal amount)
    {
        return amount - CalculatePlatformFee(amount);
    }

    public bool CanRefund(Payment payment, decimal? refundAmount = null)
    {
        if (payment.Status != PaymentStatus.Approved && payment.Status != PaymentStatus.Captured)
            return false;

        var amountToRefund = refundAmount ?? payment.Amount.Amount;

        if (amountToRefund > payment.Amount.Amount)
            return false;

        var alreadyRefunded = payment.RefundedAmount?.Amount ?? 0;
        if (alreadyRefunded + amountToRefund > payment.Amount.Amount)
            return false;

        return true;
    }

    public bool CanCapture(Payment payment)
    {
        return payment.Status == PaymentStatus.Authorized;
    }

    public bool CanCancel(Payment payment)
    {
        return payment.Status == PaymentStatus.Pending ||
               payment.Status == PaymentStatus.Authorized;
    }

    public bool IsExpired(Payment payment)
    {
        return payment.IsExpired;
    }

    public void ValidatePaymentForProcessing(Payment payment)
    {
        if (payment.Status != PaymentStatus.Pending && payment.Status != PaymentStatus.Authorized)
        {
            throw new InvalidOperationException($"Payment status must be Pending or Authorized to process. Current status: {payment.Status}");
        }

        if (payment.IsExpired)
        {
            payment.Cancel();
            throw new InvalidOperationException("Payment has expired");
        }
    }
}
