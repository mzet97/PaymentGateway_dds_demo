using PaymentGateway.Domain.Entities;

namespace PaymentGateway.Application.UseCases.Payments.ViewModels;

internal static class PaymentViewModelMapper
{
    public static PaymentDto ToDto(Payment payment, bool redactCustomerDocument = false)
    {
        ArgumentNullException.ThrowIfNull(payment);

        return new PaymentDto
        {
            Id = payment.Id,
            MerchantId = payment.MerchantId,
            Amount = payment.Amount.Amount,
            Currency = payment.Amount.Currency,
            Status = payment.Status.ToString().ToLowerInvariant(),
            Method = payment.Method.ToString().ToLowerInvariant(),
            Customer = new CustomerDto
            {
                Email = payment.Customer.Email,
                Name = payment.Customer.Name,
                Document = redactCustomerDocument
                    ? RedactDocument(payment.Customer.Document)
                    : payment.Customer.Document,
                Ip = payment.Customer.Ip,
                Phone = payment.Customer.Phone
            },
            Items = payment.Items.Select(item => new PaymentItemDto
            {
                Sku = item.Sku,
                Name = item.Name,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice.Amount
            }).ToList(),
            FraudScore = payment.FraudResult?.RiskScore,
            FraudDecision = payment.FraudResult?.Decision.ToString().ToLowerInvariant(),
            TransactionId = payment.TransactionId,
            CreatedAt = payment.CreatedAt,
            ProcessedAt = payment.ProcessedAt,
            CapturedAt = payment.CapturedAt,
            CancelledAt = payment.CancelledAt,
            RefundedAt = payment.RefundedAt,
            ExpiresAt = payment.ExpiresAt,
            RefundedAmount = payment.RefundedAmount?.Amount,
            Metadata = payment.Metadata.ToDictionary(kv => kv.Key, kv => kv.Value)
        };
    }

    private static string RedactDocument(string? document)
    {
        if (string.IsNullOrWhiteSpace(document))
            return string.Empty;

        return document.Length > 3
            ? document[..3] + "***"
            : document;
    }
}
