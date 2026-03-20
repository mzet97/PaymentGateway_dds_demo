using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PaymentGateway.Infrastructure.Persistence.Mongo.Entities;

public class PendingPaymentEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("merchantId")]
    public Guid MerchantId { get; set; }

    [BsonElement("amount")]
    public decimal Amount { get; set; }

    [BsonElement("currency")]
    public string Currency { get; set; } = "BRL";

    [BsonElement("status")]
    public string Status { get; set; } = "pending";

    [BsonElement("method")]
    public string Method { get; set; } = "credit_card";

    [BsonElement("customer")]
    public CustomerDocument Customer { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("items")]
    public List<PaymentItemDocument> Items { get; set; } = new();

    [BsonElement("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();

    [BsonElement("fraudScore")]
    public decimal? FraudScore { get; set; }

    [BsonElement("fraudDecision")]
    public string? FraudDecision { get; set; }

    [BsonElement("transactionId")]
    public string? TransactionId { get; set; }

    [BsonElement("idempotencyKey")]
    public string? IdempotencyKey { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("processedAt")]
    public DateTime? ProcessedAt { get; set; }

    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("capturedAt")]
    public DateTime? CapturedAt { get; set; }

    [BsonElement("refundedAt")]
    public DateTime? RefundedAt { get; set; }

    [BsonElement("refundedAmount")]
    public decimal? RefundedAmount { get; set; }
}
