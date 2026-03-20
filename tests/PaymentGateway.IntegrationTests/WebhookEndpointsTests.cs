using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace PaymentGateway.IntegrationTests;

public class WebhookEndpointsTests : IClassFixture<PaymentGatewayWebApplicationFactory>
{
    private readonly PaymentGatewayWebApplicationFactory _factory;

    public WebhookEndpointsTests(PaymentGatewayWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateListAndDeleteWebhook_ForOwnMerchant_Works()
    {
        using var client = _factory.CreateClient()
            .WithTestIdentity("Merchant", IntegrationTestDefaults.DefaultMerchantId);

        var createRequest = new
        {
            merchantId = IntegrationTestDefaults.DefaultMerchantId,
            url = $"https://webhook-{Guid.NewGuid():N}.example.test/receive",
            events = new[] { "payment.approved", "payment.refunded" },
            secret = "integration-secret",
            active = true
        };

        var createResponse = await client.PutAsJsonAsync("/api/v1/webhooks", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var createdPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var webhookId = createdPayload.RootElement.GetProperty("webhookId").GetGuid();

        var listResponse = await client.GetAsync($"/api/v1/webhooks?merchantId={IntegrationTestDefaults.DefaultMerchantId}");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var listPayload = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        listPayload.RootElement.EnumerateArray()
            .Select(element => element.GetProperty("webhookId").GetGuid())
            .Should()
            .Contain(webhookId);

        var deleteResponse = await client.DeleteAsync($"/api/v1/webhooks/{webhookId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CreateWebhook_ForAnotherMerchant_ReturnsForbidden()
    {
        using var client = _factory.CreateClient()
            .WithTestIdentity("Merchant", IntegrationTestDefaults.DefaultMerchantId);

        var request = new
        {
            merchantId = IntegrationTestDefaults.SecondaryMerchantId,
            url = "https://forbidden-webhook.example.test/receive",
            events = new[] { "payment.approved" },
            active = true
        };

        var response = await client.PutAsJsonAsync("/api/v1/webhooks", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
