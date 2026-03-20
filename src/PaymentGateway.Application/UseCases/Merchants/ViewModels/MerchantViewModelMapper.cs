using PaymentGateway.Domain.Entities;

namespace PaymentGateway.Application.UseCases.Merchants.ViewModels;

internal static class MerchantViewModelMapper
{
    public static MerchantDto ToDto(Merchant merchant)
    {
        ArgumentNullException.ThrowIfNull(merchant);

        return new MerchantDto
        {
            MerchantId = merchant.Id,
            Name = merchant.Name,
            Email = merchant.Email,
            Document = merchant.Document,
            Category = merchant.Category,
            Status = merchant.Status,
            CreatedAt = merchant.CreatedAt,
            CallbackUrl = merchant.CallbackUrl
        };
    }
}
