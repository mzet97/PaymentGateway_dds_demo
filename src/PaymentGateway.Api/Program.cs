using PaymentGateway.Api.Configuration;
using PaymentGateway.Api.Endpoints;
using PaymentGateway.Infrastructure.Configuration;
using PaymentGateway.Infrastructure.Persistence.Postgres;

var builder = WebApplication.CreateBuilder(args);
builder.AddPaymentGatewayServices();

var app = builder.Build();
await DatabaseInitializer.InitializeDatabaseAsync(app.Services);
app.UsePaymentGatewayPipeline();

app.MapHealthEndpoints()
    .MapPaymentUpdatesEndpoints()
    .MapPaymentsEndpoints()
    .MapTransactionsEndpoints()
    .MapWebhooksEndpoints()
    .MapMerchantsEndpoints()
    .MapStatisticsEndpoints();

Console.WriteLine("Payment Gateway API starting...");
Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");

app.Run();

public partial class Program { }
