using Paramore.Darker;
using PaymentGateway.Application.UseCases.Payments.ViewModels;

namespace PaymentGateway.Application.UseCases.Payments.Queries;

public sealed class GetPaymentByIdQuery : IQuery<PaymentDto?>
{
    public GetPaymentByIdQuery(Guid paymentId)
    {
        PaymentId = paymentId;
    }

    public Guid PaymentId { get; }
}
