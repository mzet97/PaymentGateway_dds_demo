using Paramore.Darker;
using PaymentGateway.Application.UseCases.Webhooks.Queries;
using PaymentGateway.Application.UseCases.Webhooks.ViewModels;
using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.Application.UseCases.Webhooks.Queries.Handlers;

public sealed class GetWebhooksQueryHandler : QueryHandlerAsync<GetWebhooksQuery, List<WebhookDto>>
{
    private readonly IWebhookRepository _webhookRepository;

    public GetWebhooksQueryHandler(IWebhookRepository webhookRepository)
    {
        _webhookRepository = webhookRepository;
    }

    public override async Task<List<WebhookDto>> ExecuteAsync(GetWebhooksQuery query, CancellationToken cancellationToken = default)
    {
        var webhooks = await _webhookRepository.GetByMerchantAsync(query.MerchantId, cancellationToken);
        return webhooks.Select(WebhookViewModelMapper.ToDto).ToList();
    }
}
