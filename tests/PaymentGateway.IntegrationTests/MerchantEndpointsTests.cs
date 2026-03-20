using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace PaymentGateway.IntegrationTests;

public class MerchantEndpointsTests : IClassFixture<PaymentGatewayWebApplicationFactory>
{
    private readonly PaymentGatewayWebApplicationFactory _factory;

    public MerchantEndpointsTests(PaymentGatewayWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateMerchant_AsAdmin_ReturnsCreated()
    {
        using var client = _factory.CreateClient()
            .WithTestIdentity("admin", IntegrationTestDefaults.AdminSubjectId);

        var request = new
        {
            name = "Merchant Admin Created",
            email = "merchant-admin@example.test",
            document = "12345678901234",
            category = "services",
            callbackUrl = "https://merchant-admin.example.test/webhook"
        };

        var response = await client.PostAsJsonAsync("/api/v1/merchants", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("merchantId").GetGuid().Should().NotBeEmpty();
        payload.RootElement.GetProperty("apiKey").GetString().Should().StartWith("pk_");
    }

    [Fact]
    public async Task GetMerchant_OwnMerchant_ReturnsOk()
    {
        using var client = _factory.CreateClient()
            .WithTestIdentity("Merchant", IntegrationTestDefaults.DefaultMerchantId);

        var response = await client.GetAsync($"/api/v1/merchants/{IntegrationTestDefaults.DefaultMerchantId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("merchantId").GetGuid().Should().Be(IntegrationTestDefaults.DefaultMerchantId);
    }

    [Fact]
    public async Task UpdateMerchant_FromAnotherMerchant_ReturnsForbidden()
    {
        using var client = _factory.CreateClient()
            .WithTestIdentity("Merchant", IntegrationTestDefaults.DefaultMerchantId);

        var request = new
        {
            name = "Blocked Update",
            email = "blocked-update@example.test",
            category = "services",
            callbackUrl = "https://blocked.example.test/webhook"
        };

        var response = await client.PutAsJsonAsync($"/api/v1/merchants/{IntegrationTestDefaults.SecondaryMerchantId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
