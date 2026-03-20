using FluentValidation;
using PaymentGateway.Application.UseCases.Payments.Commands;

namespace PaymentGateway.Application.UseCases.Payments.Commands.Validators;

public sealed class RefundPaymentCommandValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId)
            .NotEmpty();

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .When(x => x.Amount.HasValue);

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Reason));
    }
}
