using FluentValidation;
using PaymentGateway.Application.UseCases.Payments.Commands;

namespace PaymentGateway.Application.UseCases.Payments.Commands.Validators;

public sealed class ApprovePaymentCommandValidator : AbstractValidator<ApprovePaymentCommand>
{
    public ApprovePaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId)
            .NotEmpty();

        RuleFor(x => x.TransactionId)
            .MaximumLength(128)
            .When(x => !string.IsNullOrWhiteSpace(x.TransactionId));
    }
}
