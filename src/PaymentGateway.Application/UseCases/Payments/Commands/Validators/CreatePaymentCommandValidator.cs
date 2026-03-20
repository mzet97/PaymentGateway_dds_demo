using FluentValidation;
using PaymentGateway.Application.UseCases.Payments.Commands;

namespace PaymentGateway.Application.UseCases.Payments.Commands.Validators;

public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.MerchantId)
            .NotEmpty();

        RuleFor(x => x.Amount)
            .GreaterThan(0);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3);

        RuleFor(x => x.Method)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(x => x.Customer)
            .NotNull()
            .SetValidator(new CustomerDtoValidator());

        RuleForEach(x => x.Items!)
            .SetValidator(new PaymentItemDtoValidator())
            .When(x => x.Items is { Count: > 0 });

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(128)
            .When(x => !string.IsNullOrWhiteSpace(x.IdempotencyKey));
    }
}
