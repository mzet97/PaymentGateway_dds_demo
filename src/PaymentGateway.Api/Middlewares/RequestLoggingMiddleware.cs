using System.Diagnostics;

namespace PaymentGateway.Api.Middlewares;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        context.Items["RequestId"] = requestId;
        context.Response.Headers["X-Request-Id"] = requestId;

        _logger.LogInformation(
            "HTTP {Method} {Path} started - RequestId: {RequestId}, TraceId: {TraceId}, IP: {IP}, UserAgent: {UserAgent}",
            context.Request.Method,
            context.Request.Path,
            requestId,
            Activity.Current?.TraceId.ToString(),
            context.Connection.RemoteIpAddress,
            context.Request.Headers["User-Agent"].ToString());

        try
        {
            await _next(context);
            stopwatch.Stop();

            _logger.LogInformation(
                "HTTP {Method} {Path} completed - RequestId: {RequestId}, TraceId: {TraceId}, StatusCode: {StatusCode}, Duration: {Duration}ms",
                context.Request.Method,
                context.Request.Path,
                requestId,
                Activity.Current?.TraceId.ToString(),
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "HTTP {Method} {Path} failed - RequestId: {RequestId}, TraceId: {TraceId}, Duration: {Duration}ms, Error: {Error}",
                context.Request.Method,
                context.Request.Path,
                requestId,
                Activity.Current?.TraceId.ToString(),
                stopwatch.ElapsedMilliseconds,
                ex.Message);
            throw;
        }
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
        => app.UseMiddleware<RequestLoggingMiddleware>();
}
