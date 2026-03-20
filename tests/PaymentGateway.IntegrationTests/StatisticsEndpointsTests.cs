using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace PaymentGateway.IntegrationTests;

public class StatisticsEndpointsTests : IClassFixture<PaymentGatewayWebApplicationFactory>
{
    private readonly PaymentGatewayWebApplicationFactory _factory;

    public StatisticsEndpointsTests(PaymentGatewayWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Statistics_ForMerchantWindow_ReturnsCreatedPayments()
    {
        using var merchantClient = _factory.CreateClient()
            .WithTestIdentity("Merchant", IntegrationTestDefaults.DefaultMerchantId);

        var from = DateTime.UtcNow.AddSeconds(-5);
        await CreatePaymentAsync(merchantClient, 31.50m, "pix", "merchant-window-1@example.test");
        await CreatePaymentAsync(merchantClient, 42.75m, "credit_card", "merchant-window-2@example.test");
        var to = DateTime.UtcNow.AddSeconds(5);

        var response = await merchantClient.GetAsync(
            $"/api/v1/statistics?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}&groupBy=day");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("totalTransactions").GetInt32().Should().BeGreaterOrEqualTo(2);
        payload.RootElement.GetProperty("byMethod").TryGetProperty("pix", out _).Should().BeTrue();
        payload.RootElement.GetProperty("groupedData").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Statistics_AsAdminWithoutMerchantId_ReturnsGlobalData()
    {
        using var merchantOneClient = _factory.CreateClient()
            .WithTestIdentity("Merchant", IntegrationTestDefaults.DefaultMerchantId);
        using var merchantTwoClient = _factory.CreateClient()
            .WithTestIdentity("Merchant", IntegrationTestDefaults.SecondaryMerchantId);
        using var adminClient = _factory.CreateClient()
            .WithTestIdentity("admin", IntegrationTestDefaults.AdminSubjectId);

        var from = DateTime.UtcNow.AddSeconds(-5);
        await CreatePaymentAsync(merchantOneClient, 19.99m, "pix", "global-1@example.test");
        await CreatePaymentAsync(merchantTwoClient, 29.99m, "credit_card", "global-2@example.test");
        var to = DateTime.UtcNow.AddSeconds(5);

        var response = await adminClient.GetAsync(
            $"/api/v1/statistics?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("totalTransactions").GetInt32().Should().BeGreaterOrEqualTo(2);
        payload.RootElement.GetProperty("totalAmount").GetDecimal().Should().BeGreaterThan(0);
    }

    private static async Task CreatePaymentAsync(HttpClient client, decimal amount, string method, string email)
    {
        var request = new
        {
            amount,
            currency = "BRL",
            method,
            customer = new
            {
                email,
                name = "Statistics Test",
                document = "12345678901"
            }
        };

        var response = await client.PostAsJsonAsync("/api/v1/payments", request);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}
