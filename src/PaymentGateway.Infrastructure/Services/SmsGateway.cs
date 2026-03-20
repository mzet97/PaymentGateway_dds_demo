using Microsoft.Extensions.Logging;

namespace PaymentGateway.Infrastructure.Services;

public interface ISmsGateway
{
    Task<bool> SendSmsAsync(string to, string message, CancellationToken ct = default);
}

public class SmsGateway : ISmsGateway
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string? _senderId;
    private readonly ILogger<SmsGateway> _logger;

    public SmsGateway(HttpClient httpClient, ILogger<SmsGateway> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = Environment.GetEnvironmentVariable("SMS_API_KEY");
        _senderId = Environment.GetEnvironmentVariable("SMS_SENDER_ID");
    }

    public async Task<bool> SendSmsAsync(string to, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("SMS_API_KEY not configured, skipping SMS");
            return false;
        }

        try
        {
            // Example: Twilio, AWS SNS, or other SMS provider
            // For demo, we'll simulate
            _logger.LogInformation("Sending SMS to {To}: {Message}", to, message);

            // In production: call actual SMS API
            // var response = await _httpClient.PostAsync(...)

            await Task.Delay(50, ct); // Simulate API call
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {To}", to);
            return false;
        }
    }
}
