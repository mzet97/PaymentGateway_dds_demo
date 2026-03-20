using FluentValidation;
using PaymentGateway.Application.UseCases.Webhooks.Commands;

namespace PaymentGateway.Application.UseCases.Webhooks.Commands.Validators;

public sealed class CreateWebhookCommandValidator : AbstractValidator<CreateWebhookCommand>
{
    public CreateWebhookCommandValidator()
    {
        RuleFor(x => x.MerchantId)
            .NotEmpty();

        RuleFor(x => x.Url)
            .NotEmpty()
            .Must(BeAbsoluteUrl)
            .WithMessage("Url must be a valid absolute URL.");

        RuleFor(x => x.Events)
            .NotEmpty();

        RuleForEach(x => x.Events)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Secret)
            .MaximumLength(128)
            .When(x => !string.IsNullOrWhiteSpace(x.Secret));
    }

    private static bool BeAbsoluteUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out _);
    }
}
