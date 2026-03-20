using System.Diagnostics;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Infrastructure.Persistence.Mongo;
using PaymentGateway.Infrastructure.Persistence.Postgres;
using PaymentGateway.Infrastructure.Persistence.Postgres.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Quartz;


public class MongoSyncJob : IJob
{
    private readonly IMongoDatabase _mongoDb;
    private readonly PaymentDbContext _dbContext;
    private readonly ILogger<MongoSyncJob> _logger;

    public MongoSyncJob(IMongoDatabase mongoDb, PaymentDbContext dbContext, ILogger<MongoSyncJob> logger)
    {
        _mongoDb = mongoDb;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "sync",
            "mongo_to_postgres",
            ActivityKind.Internal);

        _logger.LogInformation("Starting MongoDB to PostgreSQL sync...");
        var sw = Stopwatch.StartNew();

        try
        {
            var collection = _mongoDb.GetCollection<BsonDocument>("pending_payments");
            var mongoDocs = await collection.Find(FilterDefinition<BsonDocument>.Empty)
                .ToListAsync(context.CancellationToken);

            _logger.LogInformation("Found {Count} documents in MongoDB", mongoDocs.Count);

            int inserted = 0, updated = 0, errors = 0;

            foreach (var doc in mongoDocs)
            {
                try
                {
                    var id = Guid.Parse(doc["_id"].AsString);
                    var merchantId = ParseMerchantId(doc.GetValue("merchantId", BsonNull.Value));

                    var existing = await _dbContext.Payments.FindAsync(new object[] { id }, context.CancellationToken);

                    if (existing == null)
                    {
                        var entity = MapToEntity(doc, id, merchantId);
                        _dbContext.Payments.Add(entity);
                        inserted++;
                    }
                    else
                    {
                        // Update mutable fields
                        existing.Status = doc.GetValue("status", "pending").AsString;
                        existing.FraudScore = doc.Contains("fraudScore") && !doc["fraudScore"].IsBsonNull
                            ? (decimal?)Convert.ToDecimal(doc["fraudScore"].ToString())
                            : null;
                        existing.FraudDecision = doc.GetValue("fraudDecision", BsonNull.Value).IsBsonNull
                            ? null : doc["fraudDecision"].AsString;
                        existing.ProcessedAt = doc.Contains("processedAt") && !doc["processedAt"].IsBsonNull
                            ? doc["processedAt"].ToUniversalTime() : null;
                        existing.TransactionId = doc.GetValue("transactionId", BsonNull.Value).IsBsonNull
                            ? null : doc["transactionId"].AsString;
                        existing.CapturedAt = doc.Contains("capturedAt") && !doc["capturedAt"].IsBsonNull
                            ? doc["capturedAt"].ToUniversalTime() : null;
                        existing.RefundedAt = doc.Contains("refundedAt") && !doc["refundedAt"].IsBsonNull
                            ? doc["refundedAt"].ToUniversalTime() : null;
                        existing.RefundedAmount = doc.Contains("refundedAmount") && !doc["refundedAmount"].IsBsonNull
                            ? Convert.ToDecimal(doc["refundedAmount"].ToString()) : null;
                        updated++;
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    if (errors <= 3)
                        _logger.LogWarning(ex, "Error syncing document {Id}", doc.GetValue("_id", "?"));
                }
            }

            await _dbContext.SaveChangesAsync(context.CancellationToken);

            // Sync transaction events
            int txInserted = 0;
            try
            {
                var txCollection = _mongoDb.GetCollection<BsonDocument>("transaction_events");
                var txDocs = await txCollection.Find(FilterDefinition<BsonDocument>.Empty)
                    .ToListAsync(context.CancellationToken);

                foreach (var txDoc in txDocs)
                {
                    try
                    {
                        var txId = Guid.Parse(txDoc["_id"].AsString);
                        var existing = await _dbContext.Transactions.FindAsync(new object[] { txId }, context.CancellationToken);
                        if (existing != null) continue;

                        var paymentId = ParseMerchantId(txDoc.GetValue("paymentId", BsonNull.Value));

                        // Parse payload for amount/currency
                        decimal amount = 0;
                        string currency = "BRL";
                        string? reference = null;
                        try
                        {
                            var payload = txDoc.GetValue("payloadJson", "{}").AsString;
                            using var jsonDoc = System.Text.Json.JsonDocument.Parse(payload);
                            if (jsonDoc.RootElement.TryGetProperty("amount", out var amtEl))
                                amount = amtEl.GetDecimal();
                            if (jsonDoc.RootElement.TryGetProperty("currency", out var curEl))
                                currency = curEl.GetString() ?? "BRL";
                            if (jsonDoc.RootElement.TryGetProperty("transactionId", out var refEl))
                                reference = refEl.GetString();
                        }
                        catch { }

                        _dbContext.Transactions.Add(new TransactionEntity
                        {
                            Id = txId,
                            PaymentId = paymentId,
                            Type = txDoc.GetValue("eventType", "unknown").AsString,
                            Amount = amount,
                            Currency = currency,
                            Reference = reference,
                            Success = true,
                            CreatedAt = txDoc.Contains("occurredAt") && !txDoc["occurredAt"].IsBsonNull
                                ? txDoc["occurredAt"].ToUniversalTime() : DateTime.UtcNow,
                        });
                        txInserted++;
                    }
                    catch { }
                }

                await _dbContext.SaveChangesAsync(context.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error syncing transaction events");
            }

            sw.Stop();
            _logger.LogInformation(
                "MongoDB to PostgreSQL sync completed in {Duration}ms: {Inserted} inserted, {Updated} updated, {Errors} errors, {TxInserted} transactions",
                sw.ElapsedMilliseconds, inserted, updated, errors, txInserted);
        }
        catch (Exception ex)
        {
            operation.RecordException(ex);
            _logger.LogError(ex, "Error during MongoDB to PostgreSQL sync");
            throw;
        }
    }

    private static Guid ParseMerchantId(BsonValue value)
    {
        if (value.IsBsonNull) return Guid.Empty;
        if (value.IsBsonBinaryData)
        {
            var bytes = value.AsBsonBinaryData.Bytes;
            if (bytes.Length == 16) return new Guid(bytes);
        }
        if (value.IsString && Guid.TryParse(value.AsString, out var guid)) return guid;
        return Guid.Empty;
    }

    private static PaymentEntity MapToEntity(BsonDocument doc, Guid id, Guid merchantId)
    {
        var customer = doc.GetValue("customer", new BsonDocument()).AsBsonDocument;

        return new PaymentEntity
        {
            Id = id,
            MerchantId = merchantId,
            Amount = Convert.ToDecimal(doc.GetValue("amount", 0).ToString()),
            Currency = doc.GetValue("currency", "BRL").AsString,
            Status = doc.GetValue("status", "pending").AsString,
            Method = doc.GetValue("method", "").AsString,
            Description = doc.GetValue("description", BsonNull.Value).IsBsonNull ? null : doc["description"].AsString,
            CustomerEmail = customer.GetValue("email", "").AsString,
            CustomerName = customer.GetValue("name", "").AsString,
            CustomerDocument = customer.GetValue("document", BsonNull.Value).IsBsonNull ? null : customer["document"].AsString,
            CustomerIp = customer.GetValue("ip", BsonNull.Value).IsBsonNull ? null : customer["ip"].AsString,
            CustomerPhone = customer.GetValue("phone", BsonNull.Value).IsBsonNull ? null : customer["phone"].AsString,
            FraudScore = doc.Contains("fraudScore") && !doc["fraudScore"].IsBsonNull
                ? (decimal?)Convert.ToDecimal(doc["fraudScore"].ToString()) : null,
            FraudDecision = doc.GetValue("fraudDecision", BsonNull.Value).IsBsonNull ? null : doc["fraudDecision"].AsString,
            TransactionId = doc.GetValue("transactionId", BsonNull.Value).IsBsonNull ? null : doc["transactionId"].AsString,
            IdempotencyKey = doc.GetValue("idempotencyKey", BsonNull.Value).IsBsonNull ? null : doc["idempotencyKey"].AsString,
            CreatedAt = doc.Contains("createdAt") && !doc["createdAt"].IsBsonNull ? doc["createdAt"].ToUniversalTime() : DateTime.UtcNow,
            ProcessedAt = doc.Contains("processedAt") && !doc["processedAt"].IsBsonNull ? doc["processedAt"].ToUniversalTime() : null,
            ExpiresAt = doc.Contains("expiresAt") && !doc["expiresAt"].IsBsonNull ? doc["expiresAt"].ToUniversalTime() : DateTime.UtcNow.AddMinutes(30),
            CapturedAt = doc.Contains("capturedAt") && !doc["capturedAt"].IsBsonNull ? doc["capturedAt"].ToUniversalTime() : null,
            RefundedAt = doc.Contains("refundedAt") && !doc["refundedAt"].IsBsonNull ? doc["refundedAt"].ToUniversalTime() : null,
            RefundedAmount = doc.Contains("refundedAmount") && !doc["refundedAmount"].IsBsonNull
                ? Convert.ToDecimal(doc["refundedAmount"].ToString()) : null,
        };
    }
}
