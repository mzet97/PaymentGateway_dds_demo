namespace PaymentGateway.Api.Common.Authorization;

internal static class MerchantScopeResolutionExtensions
{
    public static IResult? ToErrorResult(this MerchantScopeResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        return resolution.Failure switch
        {
            MerchantScopeFailure.Forbidden => Results.Forbid(),
            MerchantScopeFailure.MissingMerchantId => Results.BadRequest(new { error = "merchantId is required" }),
            _ => null
        };
    }
}
