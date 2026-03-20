using MongoDB.Bson.Serialization.Attributes;

namespace PaymentGateway.Infrastructure.Persistence.Mongo.Entities;

public class CustomerDocument
{
    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("document")]
    public string Document { get; set; } = string.Empty;

    [BsonElement("ip")]
    public string? Ip { get; set; }

    [BsonElement("phone")]
    public string? Phone { get; set; }
}
