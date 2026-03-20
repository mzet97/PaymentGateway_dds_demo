namespace PaymentGateway.Infrastructure.Configuration;

public sealed record InfrastructureRegistration(
    string MongoConnection,
    string MongoDatabase,
    string PostgresConnection,
    string RedisConnection,
    string RedisInstanceName,
    bool UseRealDds,
    // PRODUCTION: DDS Telemetry & Circuit Breaker configuration
    float DdsTelemetrySamplingRate = 0.1f,
    int CircuitBreakerFailureThreshold = 5,
    int CircuitBreakerRecoveryIntervalSeconds = 30);
