using Paramore.Darker;
using PaymentGateway.Application.UseCases.Payments.Queries;
using PaymentGateway.Application.UseCases.Payments.ViewModels;
using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.Application.UseCases.Payments.Queries.Handlers;

public sealed class GetPaymentsQueryHandler : QueryHandlerAsync<GetPaymentsQuery, PaymentsListResult>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetPaymentsQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public override async Task<PaymentsListResult> ExecuteAsync(
        GetPaymentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var payments = await _paymentRepository.GetByMerchantAsync(
            query.MerchantId ?? Guid.Empty,
            query.Status,
            query.From,
            query.To,
            query.Limit,
            query.Offset,
            cancellationToken);

        var total = await _paymentRepository.CountByMerchantAsync(
            query.MerchantId ?? Guid.Empty,
            query.Status,
            query.From,
            query.To,
            cancellationToken);

        return new PaymentsListResult
        {
            Items = payments.Select(payment => PaymentViewModelMapper.ToDto(payment)).ToList(),
            Total = total,
            Limit = query.Limit,
            Offset = query.Offset,
            HasMore = query.Offset + query.Limit < total
        };
    }
}
