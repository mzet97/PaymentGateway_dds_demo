using MongoDB.Driver;
using PaymentGateway.Infrastructure.Persistence.Mongo.Entities;

namespace PaymentGateway.Infrastructure.Persistence.Mongo;

public interface IMongoDbContext
{
    IMongoCollection<PendingPaymentEntity> PendingPayments { get; }
    IMongoCollection<WebhookEntity> Webhooks { get; }
    IMongoCollection<TransactionEventEntity> TransactionEvents { get; }
}

public class MongoDbContext : IMongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(string connectionString, string databaseName)
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<PendingPaymentEntity> PendingPayments =>
        _database.GetCollection<PendingPaymentEntity>("pending_payments");

    public IMongoCollection<WebhookEntity> Webhooks =>
        _database.GetCollection<WebhookEntity>("webhooks");

    public IMongoCollection<TransactionEventEntity> TransactionEvents =>
        _database.GetCollection<TransactionEventEntity>("transaction_events");
}
