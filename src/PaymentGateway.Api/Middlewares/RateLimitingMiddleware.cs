using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using PaymentGateway.Domain.Observability;
using StackExchange.Redis;

namespace PaymentGateway.Api.Middlewares;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "middleware",
            "rate_limit",
            ActivityKind.Internal,
            ("path", context.Request.Path.ToString()),
            ("method", context.Request.Method));

        var services = context.RequestServices;
        var connection = services.GetRequiredService<IConnectionMultiplexer>();
        var db = connection.GetDatabase();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Get client identifier (API key, JWT subject, or IP)
        var clientId = GetClientIdentifier(context);
        var endpoint = context.Request.Path;

        // Get tier limits
        var tier = GetTierFromContext(context);
        var limits = GetTierLimits(tier);

        // Apply rate limiting per endpoint
        foreach (var (window, requestsAllowed) in limits)
        {
            var key = $"ratelimit:{tier}:{window}:{clientId}:{endpoint}";

            try
            {
                var windowMs = window * 1000d;
                var minScore = now - windowMs;

                // Remove old entries
                await db.SortedSetRemoveRangeByScoreAsync(key, 0, minScore);

                // Count current requests
                var requestCount = await db.SortedSetLengthAsync(key);

                if (requestCount >= requestsAllowed)
                {
                    operation.SetResult("rate_limited");
                    var retryAfter = await CalculateRetryAfterAsync(db, key, now, windowMs);

                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Retry-After"] = retryAfter.ToString();

                    var response = new
                    {
                        error = "Too Many Requests",
                        message = $"Rate limit exceeded for {window}s window",
                        retryAfterSeconds = retryAfter
                    };

                    await context.Response.WriteAsJsonAsync(response);
                    return;
                }

                // Add current request
                var member = $"{now}:{Guid.NewGuid():N}";
                await db.SortedSetAddAsync(key, member, now);
                await db.KeyExpireAsync(key, TimeSpan.FromMilliseconds(windowMs * 2));

                // Add rate limit headers
                context.Response.Headers["X-RateLimit-Limit"] = requestsAllowed.ToString();
                context.Response.Headers["X-RateLimit-Remaining"] = (requestsAllowed - requestCount - 1).ToString();
                var resetAt = DateTimeOffset.FromUnixTimeMilliseconds((long)(now + windowMs)).ToUnixTimeSeconds();
                context.Response.Headers["X-RateLimit-Reset"] = resetAt.ToString();
            }
            catch (Exception ex)
            {
                operation.RecordException(ex);
                _logger.LogWarning(ex, "Rate limiting error, allowing request");
            }
        }

        await _next(context);
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        // Try API key first
        if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKey) && !StringValues.IsNullOrEmpty(apiKey))
        {
            return $"apikey:{apiKey}";
        }

        // Try JWT subject
        var userId = context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        // Fall back to IP
        return $"ip:{context.Connection.RemoteIpAddress}";
    }

    private static string GetTierFromContext(HttpContext context)
    {
        // Check for tier claim in JWT
        var tier = context.User.FindFirst("tier")?.Value;
        if (!string.IsNullOrEmpty(tier))
        {
            return tier;
        }

        // Default to Free tier
        return "Free";
    }

    private static IEnumerable<(int window, int requests)> GetTierLimits(string tier)
    {
        return tier switch
        {
            "Enterprise" => new[] { (1, 1000), (60, 50000), (3600, 500000) },
            "Pro" => new[] { (1, 500), (60, 20000), (3600, 200000) },
            "Basic" => new[] { (1, 100), (60, 5000), (3600, 50000) },
            "Free" => new[] { (1, 10), (60, 200), (3600, 2000) },
            _ => new[] { (1, 10), (60, 200), (3600, 2000) }
        };
    }

    private static async Task<long> CalculateRetryAfterAsync(
        IDatabase db,
        string key,
        double now,
        double windowMs)
    {
        var oldestEntry = await db.SortedSetRangeByRankWithScoresAsync(key, 0, 0, Order.Ascending);
        if (oldestEntry.Length == 0)
            return 1;

        var retryAfterMs = (oldestEntry[0].Score + windowMs) - now;
        return Math.Max(1, (long)Math.Ceiling(retryAfterMs / 1000d));
    }
}

// Extension method for adding rate limiting
public static class RateLimitingExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitingMiddleware>();
    }
}
