using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PaymentGateway.Infrastructure.Persistence.Mongo.Entities;

public class TransactionEventEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("paymentId")]
    public Guid PaymentId { get; set; }

    [BsonElement("merchantId")]
    public Guid MerchantId { get; set; }

    [BsonElement("eventType")]
    public string EventType { get; set; } = string.Empty;

    [BsonElement("payloadJson")]
    public string PayloadJson { get; set; } = "{}";

    [BsonElement("occurredAt")]
    public DateTime OccurredAt { get; set; }
}
