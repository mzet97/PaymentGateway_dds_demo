using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PaymentGateway.Infrastructure.Persistence.Mongo.Entities;

public class WebhookEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("merchantId")]
    public Guid MerchantId { get; set; }

    [BsonElement("url")]
    public string Url { get; set; } = string.Empty;

    [BsonElement("events")]
    public List<string> Events { get; set; } = new();

    [BsonElement("secret")]
    public string Secret { get; set; } = string.Empty;

    [BsonElement("isActive")]
    public bool IsActive { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}
