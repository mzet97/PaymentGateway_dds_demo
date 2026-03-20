using FluentValidation;
using PaymentGateway.Application.UseCases.Webhooks.Commands;

namespace PaymentGateway.Application.UseCases.Webhooks.Commands.Validators;

public sealed class DeleteWebhookCommandValidator : AbstractValidator<DeleteWebhookCommand>
{
    public DeleteWebhookCommandValidator()
    {
        RuleFor(x => x.WebhookId)
            .NotEmpty();
    }
}
