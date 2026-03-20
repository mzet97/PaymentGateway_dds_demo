using PaymentGateway.Api.Middlewares;

namespace PaymentGateway.Api.Configuration;

public static class PipelineExtensions
{
    public static WebApplication UsePaymentGatewayPipeline(this WebApplication app)
    {
        app.UseCors();
        app.UseSecurityHeaders();
        app.UseRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseWebSockets();
        app.UseAuthentication();
        app.UseAuthorization();

        if (!app.Environment.IsEnvironment("Testing") &&
            !string.Equals(Environment.GetEnvironmentVariable("DISABLE_RATE_LIMIT"), "true", StringComparison.OrdinalIgnoreCase))
        {
            app.UseRateLimiting();
        }

        if (!app.Environment.IsEnvironment("Testing"))
        {
            app.UseIdempotency();
        }

        return app;
    }
}
