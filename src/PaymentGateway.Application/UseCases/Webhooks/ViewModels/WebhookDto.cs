using System.Text.Json.Serialization;

namespace PaymentGateway.Application.UseCases.Webhooks.ViewModels;

public sealed class WebhookDto
{
    public Guid WebhookId { get; init; }
    public Guid MerchantId { get; init; }
    public string Url { get; init; } = string.Empty;
    public IReadOnlyList<string> Events { get; init; } = Array.Empty<string>();
    [JsonPropertyName("active")]
    public bool Active { get; init; }
    public DateTime CreatedAt { get; init; }
}
