using Paramore.Darker;
using PaymentGateway.Application.UseCases.Payments.Queries;
using PaymentGateway.Application.UseCases.Payments.ViewModels;
using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.Application.UseCases.Payments.Queries.Handlers;

public sealed class GetPaymentByIdQueryHandler : QueryHandlerAsync<GetPaymentByIdQuery, PaymentDto?>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetPaymentByIdQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public override async Task<PaymentDto?> ExecuteAsync(
        GetPaymentByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var payment = await _paymentRepository.GetByIdAsync(query.PaymentId, cancellationToken);
        return payment is null ? null : PaymentViewModelMapper.ToDto(payment, redactCustomerDocument: true);
    }
}
