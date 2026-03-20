using FluentValidation;
using PaymentGateway.Application.UseCases.Payments.ViewModels;

namespace PaymentGateway.Application.UseCases.Payments.Commands.Validators;

public sealed class PaymentItemDtoValidator : AbstractValidator<PaymentItemDto>
{
    public PaymentItemDtoValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Quantity)
            .GreaterThan(0);

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0);
    }
}
