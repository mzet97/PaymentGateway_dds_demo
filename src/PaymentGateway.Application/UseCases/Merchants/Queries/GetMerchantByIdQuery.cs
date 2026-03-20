using Paramore.Darker;
using PaymentGateway.Application.UseCases.Merchants.ViewModels;

namespace PaymentGateway.Application.UseCases.Merchants.Queries;

public sealed class GetMerchantByIdQuery : IQuery<MerchantDto?>
{
    public GetMerchantByIdQuery(Guid merchantId)
    {
        MerchantId = merchantId;
    }

    public Guid MerchantId { get; }
}
