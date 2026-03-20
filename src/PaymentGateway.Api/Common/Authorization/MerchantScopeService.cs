using System.Security.Claims;

namespace PaymentGateway.Api.Common.Authorization;

internal sealed class MerchantScopeService : IMerchantScopeService
{
    public MerchantScopeResolution Resolve(ClaimsPrincipal user, Guid? requestedMerchantId, bool allowGlobalForAdmin)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.IsGatewayAdmin())
        {
            if (!allowGlobalForAdmin && requestedMerchantId is null)
            {
                return new MerchantScopeResolution { Failure = MerchantScopeFailure.MissingMerchantId };
            }

            return new MerchantScopeResolution { MerchantId = requestedMerchantId };
        }

        if (!user.TryGetMerchantId(out var merchantIdFromClaim))
        {
            return new MerchantScopeResolution { Failure = MerchantScopeFailure.Forbidden };
        }

        if (requestedMerchantId.HasValue && requestedMerchantId.Value != merchantIdFromClaim)
        {
            return new MerchantScopeResolution { Failure = MerchantScopeFailure.Forbidden };
        }

        return new MerchantScopeResolution { MerchantId = merchantIdFromClaim };
    }
}
