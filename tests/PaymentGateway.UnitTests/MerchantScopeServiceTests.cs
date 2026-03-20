using System.Security.Claims;
using FluentAssertions;
using PaymentGateway.Api.Common.Authorization;
using Xunit;

namespace PaymentGateway.UnitTests;

public sealed class MerchantScopeServiceTests
{
    private static readonly Guid MerchantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RequestedMerchantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly MerchantScopeService _service = new();

    [Fact]
    public void Resolve_ShouldAllowAdminGlobalScope_WhenMerchantIdNotRequired()
    {
        var user = CreatePrincipal("admin", new Claim("sub", Guid.NewGuid().ToString()));

        var result = _service.Resolve(user, null, allowGlobalForAdmin: true);

        result.Failure.Should().BeNull();
        result.MerchantId.Should().BeNull();
    }

    [Fact]
    public void Resolve_ShouldRequireMerchantId_WhenAdminRequestsScopedEndpointWithoutMerchantId()
    {
        var user = CreatePrincipal("Admin", new Claim("sub", Guid.NewGuid().ToString()));

        var result = _service.Resolve(user, null, allowGlobalForAdmin: false);

        result.Failure.Should().Be(MerchantScopeFailure.MissingMerchantId);
        result.MerchantId.Should().BeNull();
    }

    [Fact]
    public void Resolve_ShouldAcceptConfiguredMerchantIdClaim_WhenSubIsNotGuid()
    {
        var user = CreatePrincipal(
            "Merchant",
            new Claim("sub", "authentik-subject"),
            new Claim("merchant_id", MerchantId.ToString()));

        var result = _service.Resolve(user, null, allowGlobalForAdmin: false);

        result.Failure.Should().BeNull();
        result.MerchantId.Should().Be(MerchantId);
    }

    [Fact]
    public void Resolve_ShouldAcceptNameIdentifierClaim_WhenItContainsMerchantId()
    {
        var user = CreatePrincipal(
            "Merchant",
            new Claim(ClaimTypes.NameIdentifier, MerchantId.ToString()),
            new Claim("sub", "authentik-subject"));

        var result = _service.Resolve(user, MerchantId, allowGlobalForAdmin: false);

        result.Failure.Should().BeNull();
        result.MerchantId.Should().Be(MerchantId);
    }

    [Fact]
    public void Resolve_ShouldRejectMerchantAccessToAnotherMerchant()
    {
        var user = CreatePrincipal(
            "Merchant",
            new Claim("merchant_id", MerchantId.ToString()),
            new Claim("sub", "authentik-subject"));

        var result = _service.Resolve(user, RequestedMerchantId, allowGlobalForAdmin: false);

        result.Failure.Should().Be(MerchantScopeFailure.Forbidden);
    }

    [Fact]
    public void Resolve_ShouldRejectMerchant_WhenNoGuidClaimExists()
    {
        var user = CreatePrincipal(
            "Merchant",
            new Claim("sub", "authentik-subject"),
            new Claim("merchant_id", "invalid-guid"));

        var result = _service.Resolve(user, null, allowGlobalForAdmin: false);

        result.Failure.Should().Be(MerchantScopeFailure.Forbidden);
    }

    [Fact]
    public void Resolve_ShouldRejectMerchant_WhenClaimContainsEmptyGuid()
    {
        var user = CreatePrincipal(
            "Merchant",
            new Claim("merchant_id", Guid.Empty.ToString()));

        var result = _service.Resolve(user, null, allowGlobalForAdmin: false);

        result.Failure.Should().Be(MerchantScopeFailure.Forbidden);
    }

    private static ClaimsPrincipal CreatePrincipal(string role, params Claim[] claims)
    {
        var allClaims = new List<Claim>(claims)
        {
            new(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(allClaims, "UnitTest");
        return new ClaimsPrincipal(identity);
    }
}
