namespace PaymentGateway.Api.Middlewares;

public static class IdempotencyExtensions
{
    public static IApplicationBuilder UseIdempotency(this IApplicationBuilder app)
    {
        return app.UseMiddleware<IdempotencyMiddleware>();
    }
}
