using MongoDB.Bson.Serialization.Attributes;

namespace PaymentGateway.Infrastructure.Persistence.Mongo.Entities;

public class PaymentItemDocument
{
    [BsonElement("sku")]
    public string Sku { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("quantity")]
    public int Quantity { get; set; }

    [BsonElement("unitPrice")]
    public decimal UnitPrice { get; set; }
}
