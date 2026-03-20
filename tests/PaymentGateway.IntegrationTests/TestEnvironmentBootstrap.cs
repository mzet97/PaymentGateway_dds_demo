using System.Threading;

namespace PaymentGateway.IntegrationTests;

internal static class TestEnvironmentBootstrap
{
    private static int _initialized;

    public static void EnsureConfigured()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        SetIfMissing(
            "ConnectionStrings__DefaultConnection",
            "Host=spsql.home.arpa;Port=5432;Database=demo-gateway;User Id=app;Password=Admin@123");
        SetIfMissing(
            "ConnectionStrings__MongoDb",
            "mongodb://admin:Admin%40123@mongodb.home.arpa:27017/?authSource=admin");
        SetIfMissing(
            "ConnectionStrings__Redis",
            "localhost:6379,password=Admin@123,abortConnect=false");
        SetIfMissing(
            "Redis__ConnectionString",
            "localhost:6379,password=Admin@123,abortConnect=false");
        SetIfMissing("Dds__UseRealDds", "false");
        SetIfMissing("SKIP_DB_INITIALIZER", "true");
        SetIfMissing("Telemetry__EnableElasticsearchLogging", "false");
        SetIfMissing("Telemetry__EnableOtlp", "false");
        SetIfMissing("Telemetry__EnableConsoleExporter", "false");
    }

    private static void SetIfMissing(string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
        {
            return;
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}
