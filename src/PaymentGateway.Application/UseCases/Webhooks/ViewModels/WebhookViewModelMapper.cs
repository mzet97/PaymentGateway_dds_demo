using PaymentGateway.Domain.Entities;

namespace PaymentGateway.Application.UseCases.Webhooks.ViewModels;

internal static class WebhookViewModelMapper
{
    public static WebhookDto ToDto(Webhook webhook)
    {
        ArgumentNullException.ThrowIfNull(webhook);

        return new WebhookDto
        {
            WebhookId = webhook.Id,
            MerchantId = webhook.MerchantId,
            Url = webhook.Url,
            Events = webhook.Events.ToList(),
            Active = webhook.IsActive,
            CreatedAt = webhook.CreatedAt
        };
    }
}
