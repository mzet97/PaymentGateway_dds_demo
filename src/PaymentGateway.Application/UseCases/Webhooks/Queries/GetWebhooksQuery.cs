using Paramore.Darker;
using PaymentGateway.Application.UseCases.Webhooks.ViewModels;

namespace PaymentGateway.Application.UseCases.Webhooks.Queries;

public sealed class GetWebhooksQuery : IQuery<List<WebhookDto>>
{
    public GetWebhooksQuery(Guid merchantId)
    {
        MerchantId = merchantId;
    }

    public Guid MerchantId { get; }
}
