using Paramore.Brighter;
using PaymentGateway.Application.Common.Behaviours;
using PaymentGateway.Application.UseCases.Webhooks.Commands;
using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.Application.UseCases.Webhooks.Commands.Handlers;

public sealed class DeleteWebhookCommandHandler : RequestHandlerAsync<DeleteWebhookCommand>
{
    private readonly IWebhookRepository _webhookRepository;

    public DeleteWebhookCommandHandler(IWebhookRepository webhookRepository)
    {
        _webhookRepository = webhookRepository;
    }

    [RequestLogging(0, HandlerTiming.Before)]
    [RequestValidation(1, HandlerTiming.Before)]
    public override async Task<DeleteWebhookCommand> HandleAsync(
        DeleteWebhookCommand command,
        CancellationToken cancellationToken = default)
    {
        await _webhookRepository.DeleteAsync(command.WebhookId, cancellationToken);
        command.Result = true;
        return await base.HandleAsync(command, cancellationToken);
    }
}
