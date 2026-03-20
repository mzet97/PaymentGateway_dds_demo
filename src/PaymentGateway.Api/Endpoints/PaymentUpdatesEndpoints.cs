using PaymentGateway.Api.Common.Realtime;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.Api.Endpoints;

public static class PaymentUpdatesEndpoints
{
    public static WebApplication MapPaymentUpdatesEndpoints(this WebApplication app)
    {
        app.Map("/ws/payments", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "WebSocket request required" });
                return;
            }

            var apiKey = context.Request.Query["apiKey"].ToString();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "apiKey query parameter is required" });
                return;
            }

            var unitOfWork = context.RequestServices.GetRequiredService<IUnitOfWork>();
            var merchant = await unitOfWork.Merchants.GetByApiKeyAsync(apiKey, context.RequestAborted);
            if (merchant == null || merchant.Status != MerchantStatus.Active)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid or inactive API key" });
                return;
            }

            var notifier = context.RequestServices.GetRequiredService<IPaymentUpdatesNotifier>();
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await notifier.HandleClientAsync(socket, merchant.Id, context.RequestAborted);
        })
        .WithTags("Payments")
        .WithName("PaymentUpdatesWebSocket");

        return app;
    }
}
