using Paramore.Darker;
using PaymentGateway.Application.UseCases.Webhooks.ViewModels;

namespace PaymentGateway.Application.UseCases.Webhooks.Queries;

public sealed class GetWebhookByIdQuery : IQuery<WebhookDto?>
{
    public GetWebhookByIdQuery(Guid webhookId)
    {
        WebhookId = webhookId;
    }

    public Guid WebhookId { get; }
}
