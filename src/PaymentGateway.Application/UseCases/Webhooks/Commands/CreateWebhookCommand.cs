using PaymentGateway.Application.Common.Messaging;
using PaymentGateway.Application.UseCases.Webhooks.ViewModels;

namespace PaymentGateway.Application.UseCases.Webhooks.Commands;

public sealed class CreateWebhookCommand : BrighterRequest<WebhookDto>
{
    public Guid MerchantId { get; set; }
    public string Url { get; set; } = string.Empty;
    public List<string> Events { get; set; } = new();
    public string? Secret { get; set; }
    public bool Active { get; set; } = true;
}
