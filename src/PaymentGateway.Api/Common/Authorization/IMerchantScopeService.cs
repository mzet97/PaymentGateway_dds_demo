using System.Security.Claims;

namespace PaymentGateway.Api.Common.Authorization;

internal interface IMerchantScopeService
{
    MerchantScopeResolution Resolve(ClaimsPrincipal user, Guid? requestedMerchantId, bool allowGlobalForAdmin);
}
