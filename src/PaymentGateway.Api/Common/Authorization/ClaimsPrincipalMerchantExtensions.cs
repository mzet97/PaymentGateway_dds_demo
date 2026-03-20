using System.Security.Claims;

namespace PaymentGateway.Api.Common.Authorization;

internal static class ClaimsPrincipalMerchantExtensions
{
    private static readonly string[] MerchantClaimTypes =
    {
        ClaimTypes.NameIdentifier,
        "merchant_id",
        "merchantId",
        "sub"
    };

    public static bool IsGatewayAdmin(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.IsInRole("Admin") || user.IsInRole("admin");
    }

    public static bool TryGetMerchantId(this ClaimsPrincipal user, out Guid merchantId)
    {
        ArgumentNullException.ThrowIfNull(user);

        foreach (var claimType in MerchantClaimTypes)
        {
            foreach (var claim in user.FindAll(claimType))
            {
                if (Guid.TryParse(claim.Value, out merchantId) && merchantId != Guid.Empty)
                {
                    return true;
                }
            }
        }

        merchantId = Guid.Empty;
        return false;
    }
}
