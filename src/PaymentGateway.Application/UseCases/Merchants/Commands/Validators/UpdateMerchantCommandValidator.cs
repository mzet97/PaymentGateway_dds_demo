using FluentValidation;
using PaymentGateway.Application.UseCases.Merchants.Commands;

namespace PaymentGateway.Application.UseCases.Merchants.Commands.Validators;

public sealed class UpdateMerchantCommandValidator : AbstractValidator<UpdateMerchantCommand>
{
    public UpdateMerchantCommandValidator()
    {
        RuleFor(x => x.MerchantId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Category)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.CallbackUrl)
            .Must(BeAbsoluteUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.CallbackUrl))
            .WithMessage("CallbackUrl must be a valid absolute URL.");
    }

    private static bool BeAbsoluteUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out _);
    }
}
