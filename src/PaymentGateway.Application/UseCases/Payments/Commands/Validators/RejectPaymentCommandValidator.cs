using FluentValidation;
using PaymentGateway.Application.UseCases.Payments.Commands;

namespace PaymentGateway.Application.UseCases.Payments.Commands.Validators;

public sealed class RejectPaymentCommandValidator : AbstractValidator<RejectPaymentCommand>
{
    public RejectPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId)
            .NotEmpty();

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(500);
    }
}
