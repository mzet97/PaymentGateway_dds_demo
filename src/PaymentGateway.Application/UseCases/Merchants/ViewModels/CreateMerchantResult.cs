using PaymentGateway.Domain.Entities;

namespace PaymentGateway.Application.UseCases.Merchants.ViewModels;

public sealed class CreateMerchantResult
{
    public Guid MerchantId { get; init; }
    public string ApiKey { get; init; } = string.Empty;
    public MerchantStatus Status { get; init; }
}
