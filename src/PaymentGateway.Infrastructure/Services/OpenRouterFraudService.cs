using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Services;
using PaymentGateway.Domain.ValueObjects;
using PaymentGateway.Infrastructure.Redis;

namespace PaymentGateway.Infrastructure.Services;

public interface IOpenRouterFraudService
{
    Task<FraudCheckResult> AnalyzePaymentAsync(
        Guid paymentId,
        decimal amount,
        string currency,
        string customerEmail,
        string customerDocument,
        string customerIp,
        CancellationToken ct = default);
}

/// <summary>
/// OpenRouter fraud detection service using MiniMax M2.5 model.
/// </summary>
public class OpenRouterFraudService : IOpenRouterFraudService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterFraudService> _logger;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly IRedisService? _cache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);

    public OpenRouterFraudService(
        HttpClient httpClient,
        ILogger<OpenRouterFraudService> logger,
        string apiKey,
        string model = "minimax/minimax-m2.5",
        IRedisService? cache = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = apiKey;
        _model = model;
        _cache = cache;
    }

    public async Task<FraudCheckResult> AnalyzePaymentAsync(
        Guid paymentId,
        decimal amount,
        string currency,
        string customerEmail,
        string customerDocument,
        string customerIp,
        CancellationToken ct = default)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "fraud",
            "openrouter_analysis",
            ActivityKind.Client,
            ("payment.id", paymentId.ToString()),
            ("provider", "openrouter"),
            ("model", _model));

        // Check cache first
        var cacheKey = $"fraud_check:{ComputeHash(paymentId)}";
        if (_cache != null)
        {
            var cached = await _cache.GetAsync<FraudCheckResult>(cacheKey);
            if (cached != null)
            {
                operation.SetResult("cache_hit");
                _logger.LogInformation("Fraud check cache hit for payment {PaymentId}", paymentId);
                return cached;
            }
        }

        try
        {
            var result = await AnalyzeWithRetryAsync(paymentId, amount, currency, customerEmail, customerDocument, customerIp, ct);

            // Cache the result
            if (_cache != null)
            {
                await _cache.SetAsync(cacheKey, result, _cacheExpiration);
            }

            operation.SetResult(result.Decision.ToString().ToLowerInvariant());
            return result;
        }
        catch (Exception ex)
        {
            operation.RecordException(ex);
            _logger.LogError(ex, "Fraud check failed for payment {PaymentId}, defaulting to review", paymentId);

            // Return review decision on failure
            return FraudCheckResult.Review(
                50,
                new List<string> { "Fraud check service unavailable" },
                new FraudMetadata(_model, 0, 0));
        }
    }

    private async Task<FraudCheckResult> AnalyzeWithRetryAsync(
        Guid paymentId,
        decimal amount,
        string currency,
        string customerEmail,
        string customerDocument,
        string customerIp,
        CancellationToken ct)
    {
        var prompt = BuildFraudAnalysisPrompt(amount, currency, customerEmail, customerDocument, customerIp);

        var request = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = "You are a fraud detection AI. Analyze payment transactions and respond with a JSON object containing: risk_score (0-100), decision ('approved', 'review', 'rejected'), and reasons (array of strings)." },
                new { role = "user", content = prompt }
            },
            max_tokens = 500,
            temperature = 0.1
        };

        var response = await ExecuteWithRetryAsync(request, ct);
        return ParseFraudResponse(paymentId, response);
    }

    private async Task<OpenRouterResponse> ExecuteWithRetryAsync(object request, CancellationToken ct)
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "http",
            "openrouter_chat_completion",
            ActivityKind.Client,
            ("provider", "openrouter"),
            ("model", _model));

        var maxRetries = 3;
        var baseDelay = TimeSpan.FromSeconds(1);

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _httpClient.PostAsJsonAsync(
                    "https://openrouter.ai/api/v1/chat/completions",
                    request,
                    ct);

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<OpenRouterResponse>(ct) ?? throw new InvalidOperationException("Empty response");
            }
            catch (Exception ex) when (attempt < maxRetries - 1)
            {
                var delay = baseDelay * Math.Pow(2, attempt);
                _logger.LogWarning(ex, "Fraud check request failed, retrying in {Delay}s", delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }

        operation.SetResult("failed_after_retries");
        throw new InvalidOperationException("Fraud check failed after retries");
    }

    private string BuildFraudAnalysisPrompt(decimal amount, string currency, string email, string document, string ip)
    {
        return string.Format(
            @"You are a payment fraud detection AI. Analyze this transaction and return ONLY a JSON object.

Transaction:
- Amount: {0} {1}
- Customer Email: {2}
- Customer Document (CPF/CNPJ): {3}
- Customer IP: {4}

Fraud analysis rules:
1. Amounts above 10000 {1} are high risk (score 70-90)
2. Amounts above 5000 {1} need review (score 40-60)
3. Suspicious email patterns (disposable domains, random chars) increase risk by 10-20
4. Missing or invalid document increases risk by 15
5. Private/local IP addresses (192.168.x.x, 10.x.x.x) are normal for testing
6. Normal transactions from known patterns get low scores (5-25)

Return ONLY this JSON (no markdown, no explanation):
{{""risk_score"": <integer 0-100>, ""decision"": ""approved"" | ""review"" | ""rejected"", ""reasons"": [""reason1"", ""reason2""]}}", amount, currency, email, document, ip);
    }

    private FraudCheckResult ParseFraudResponse(Guid paymentId, OpenRouterResponse response)
    {
        var content = response.choices?[0]?.message?.content?.Trim() ?? "";
        _logger.LogInformation("AI fraud response for {PaymentId}: {Content}", paymentId, content);

        try
        {
            // Extract JSON from response (may be wrapped in markdown code blocks)
            var json = content;
            if (json.Contains("```"))
            {
                var start = json.IndexOf('{');
                var end = json.LastIndexOf('}');
                if (start >= 0 && end > start)
                    json = json[start..(end + 1)];
            }

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var riskScore = root.TryGetProperty("risk_score", out var scoreEl)
                ? scoreEl.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? scoreEl.GetDecimal()
                    : decimal.TryParse(scoreEl.GetString(), out var parsed) ? parsed : 50m
                : 50m;

            var decision = root.TryGetProperty("decision", out var decEl)
                ? decEl.GetString()?.ToLowerInvariant() ?? "review"
                : "review";

            var reasons = new List<string>();
            if (root.TryGetProperty("reasons", out var reasonsEl) && reasonsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var r in reasonsEl.EnumerateArray())
                    reasons.Add(r.GetString() ?? "");
            }

            // Clamp score to 0-100
            riskScore = Math.Clamp(riskScore, 0, 100);

            var metadata = new FraudMetadata(_model, 0, 0.85m);

            return decision switch
            {
                "approved" => FraudCheckResult.Approved(riskScore, metadata),
                "rejected" => FraudCheckResult.Rejected(riskScore, reasons, metadata),
                _ => FraudCheckResult.Review(riskScore, reasons, metadata),
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI fraud response, falling back to text analysis. Content: {Content}", content);

            // Fallback: text-based analysis
            var lower = content.ToLowerInvariant();
            if (lower.Contains("approved") && !lower.Contains("review") && !lower.Contains("rejected"))
                return FraudCheckResult.Approved(15, new FraudMetadata(_model, 0, 0.7m));
            if (lower.Contains("rejected"))
                return FraudCheckResult.Rejected(85, new List<string> { "AI flagged as rejected" }, new FraudMetadata(_model, 0, 0.7m));

            return FraudCheckResult.Review(50, new List<string> { "Unable to parse AI response" }, new FraudMetadata(_model, 0, 0.5m));
        }
    }

    private static string ComputeHash(Guid paymentId)
    {
        var bytes = SHA256.HashData(paymentId.ToByteArray());
        return Convert.ToBase64String(bytes);
    }

    private class OpenRouterResponse
    {
        public List<Choice>? choices { get; set; }
    }

    private class Choice
    {
        public Message? message { get; set; }
    }

    private class Message
    {
        public string? content { get; set; }
    }
}
