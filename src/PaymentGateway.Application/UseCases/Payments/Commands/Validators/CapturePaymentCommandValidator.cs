using FluentValidation;
using PaymentGateway.Application.UseCases.Payments.Commands;

namespace PaymentGateway.Application.UseCases.Payments.Commands.Validators;

public sealed class CapturePaymentCommandValidator : AbstractValidator<CapturePaymentCommand>
{
    public CapturePaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId)
            .NotEmpty();

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .When(x => x.Amount.HasValue);
    }
}
