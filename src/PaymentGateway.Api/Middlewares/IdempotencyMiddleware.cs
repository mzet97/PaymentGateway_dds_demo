using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Services;

namespace PaymentGateway.Api.Middlewares;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "middleware",
            "idempotency",
            ActivityKind.Internal,
            ("path", context.Request.Path.ToString()),
            ("method", context.Request.Method));

        if (context.Request.Method != HttpMethods.Post)
        {
            operation.SetResult("skipped_method");
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var idempotencyKey) ||
            StringValues.IsNullOrEmpty(idempotencyKey))
        {
            operation.SetResult("skipped_missing_key");
            await _next(context);
            return;
        }

        var key = idempotencyKey.ToString();

        if (string.IsNullOrWhiteSpace(key) || key.Length < 10)
        {
            operation.SetResult("invalid_key");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid idempotency key" });
            return;
        }

        var redis = context.RequestServices.GetRequiredService<IRedisService>();
        var scope = ResolveScope(context);
        var requestHash = await ComputeRequestHashAsync(context.Request);
        var cacheKey = $"idempotency:{scope}:{context.Request.Method}:{context.Request.Path}:{key}";

        try
        {
            var existingResult = await redis.GetValueAsync(cacheKey);
            if (existingResult != null)
            {
                _logger.LogInformation(
                    "Idempotency key {KeySuffix} already exists, returning cached result",
                    DescribeKey(key));

                var cachedResponse = JsonSerializer.Deserialize<IdempotencyResponse>(existingResult);
                if (cachedResponse != null)
                {
                    if (!string.Equals(cachedResponse.RequestHash, requestHash, StringComparison.Ordinal))
                    {
                        operation.SetResult("conflict");
                        context.Response.StatusCode = StatusCodes.Status409Conflict;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "Idempotency key reuse with different payload is not allowed"
                        });
                        return;
                    }

                    context.Response.StatusCode = cachedResponse.StatusCode;
                    context.Response.ContentType = cachedResponse.ContentType;

                    if (!string.IsNullOrEmpty(cachedResponse.Body))
                    {
                        await context.Response.WriteAsync(cachedResponse.Body);
                    }

                    operation.SetResult("cache_hit");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            operation.RecordException(ex);
            _logger.LogWarning(ex, "Failed to read idempotency cache; continuing request without cache hit");
            await _next(context);
            return;
        }

        var originalBodyStream = context.Response.Body;
        await using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context);

            memoryStream.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();

            var idempotencyResponse = new IdempotencyResponse
            {
                StatusCode = context.Response.StatusCode,
                ContentType = context.Response.ContentType ?? "application/json",
                Body = responseBody,
                RequestHash = requestHash
            };

            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                try
                {
                    var serializedResponse = JsonSerializer.Serialize(idempotencyResponse);
                    await redis.SetValueAsync(cacheKey, serializedResponse, IdempotencyTtl);
                    _logger.LogInformation("Cached idempotent response for key {KeySuffix}", DescribeKey(key));
                }
                catch (Exception ex)
                {
                    operation.RecordException(ex);
                    _logger.LogWarning(ex, "Failed to persist idempotency cache for key {KeySuffix}", DescribeKey(key));
                }
            }

            memoryStream.Seek(0, SeekOrigin.Begin);
            await memoryStream.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private static string DescribeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "***";

        return key.Length <= 8 ? "***" : $"***{key[^4..]}";
    }

    private static string ResolveScope(HttpContext context)
    {
        var merchantId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? context.User.FindFirst("sub")?.Value;

        if (!string.IsNullOrWhiteSpace(merchantId))
            return $"merchant:{merchantId}";

        if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKey) && !StringValues.IsNullOrEmpty(apiKey))
            return $"apikey:{apiKey.ToString()}";

        return $"ip:{context.Connection.RemoteIpAddress}";
    }

    private static async Task<string> ComputeRequestHashAsync(HttpRequest request)
    {
        request.EnableBuffering();
        request.Body.Position = 0;

        using var memoryStream = new MemoryStream();
        await request.Body.CopyToAsync(memoryStream);
        request.Body.Position = 0;

        var hash = SHA256.HashData(memoryStream.ToArray());
        return Convert.ToHexString(hash);
    }
}
