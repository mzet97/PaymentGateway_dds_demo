using Paramore.Darker;
using PaymentGateway.Application.UseCases.Webhooks.Queries;
using PaymentGateway.Application.UseCases.Webhooks.ViewModels;
using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.Application.UseCases.Webhooks.Queries.Handlers;

public sealed class GetWebhookByIdQueryHandler : QueryHandlerAsync<GetWebhookByIdQuery, WebhookDto?>
{
    private readonly IWebhookRepository _webhookRepository;

    public GetWebhookByIdQueryHandler(IWebhookRepository webhookRepository)
    {
        _webhookRepository = webhookRepository;
    }

    public override async Task<WebhookDto?> ExecuteAsync(GetWebhookByIdQuery query, CancellationToken cancellationToken = default)
    {
        var webhook = await _webhookRepository.GetByIdAsync(query.WebhookId, cancellationToken);
        return webhook is null ? null : WebhookViewModelMapper.ToDto(webhook);
    }
}
