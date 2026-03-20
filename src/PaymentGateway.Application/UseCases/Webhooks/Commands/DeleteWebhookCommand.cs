using PaymentGateway.Application.Common.Messaging;

namespace PaymentGateway.Application.UseCases.Webhooks.Commands;

public sealed class DeleteWebhookCommand : BrighterRequest<bool>
{
    public Guid WebhookId { get; set; }
}
