using Paramore.Darker;
using PaymentGateway.Application.UseCases.Merchants.Queries;
using PaymentGateway.Application.UseCases.Merchants.ViewModels;
using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.Application.UseCases.Merchants.Queries.Handlers;

public sealed class GetMerchantByIdQueryHandler : QueryHandlerAsync<GetMerchantByIdQuery, MerchantDto?>
{
    private readonly IMerchantRepository _merchantRepository;

    public GetMerchantByIdQueryHandler(IMerchantRepository merchantRepository)
    {
        _merchantRepository = merchantRepository;
    }

    public override async Task<MerchantDto?> ExecuteAsync(GetMerchantByIdQuery query, CancellationToken cancellationToken = default)
    {
        var merchant = await _merchantRepository.GetByIdAsync(query.MerchantId, cancellationToken);
        return merchant is null ? null : MerchantViewModelMapper.ToDto(merchant);
    }
}
