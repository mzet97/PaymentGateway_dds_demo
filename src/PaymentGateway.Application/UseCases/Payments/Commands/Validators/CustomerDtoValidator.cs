using FluentValidation;
using PaymentGateway.Application.UseCases.Payments.ViewModels;

namespace PaymentGateway.Application.UseCases.Payments.Commands.Validators;

public sealed class CustomerDtoValidator : AbstractValidator<CustomerDto>
{
    public CustomerDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Document)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(x => x.Phone)
            .MaximumLength(32)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Ip)
            .MaximumLength(64)
            .When(x => !string.IsNullOrWhiteSpace(x.Ip));
    }
}
