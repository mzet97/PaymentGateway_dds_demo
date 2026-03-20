using Paramore.Brighter;
using PaymentGateway.Application.Common.Behaviours;
using PaymentGateway.Application.UseCases.Webhooks.Commands;
using PaymentGateway.Application.UseCases.Webhooks.ViewModels;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.Application.UseCases.Webhooks.Commands.Handlers;

public sealed class CreateWebhookCommandHandler : RequestHandlerAsync<CreateWebhookCommand>
{
    private readonly IWebhookRepository _webhookRepository;

    public CreateWebhookCommandHandler(IWebhookRepository webhookRepository)
    {
        _webhookRepository = webhookRepository;
    }

    [RequestLogging(0, HandlerTiming.Before)]
    [RequestValidation(1, HandlerTiming.Before)]
    public override async Task<CreateWebhookCommand> HandleAsync(
        CreateWebhookCommand command,
        CancellationToken cancellationToken = default)
    {
        var webhook = Webhook.Create(
            command.MerchantId,
            command.Url,
            command.Events,
            command.Secret ?? Guid.NewGuid().ToString("N"),
            command.Active);

        await _webhookRepository.AddAsync(webhook, cancellationToken);

        command.Result = WebhookViewModelMapper.ToDto(webhook);
        return await base.HandleAsync(command, cancellationToken);
    }
}
