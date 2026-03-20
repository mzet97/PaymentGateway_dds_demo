namespace PaymentGateway.Api.Common.Authorization;

internal sealed class MerchantScopeResolution
{
    public Guid? MerchantId { get; init; }
    public MerchantScopeFailure? Failure { get; init; }
}
