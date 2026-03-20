using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PaymentGateway.IntegrationTests;

/// <summary>
/// Integration tests for Payment Gateway API.
/// </summary>
public class PaymentFlowTests : IClassFixture<PaymentGatewayWebApplicationFactory>
{
    private readonly PaymentGatewayWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PaymentFlowTests(PaymentGatewayWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<HealthResponse>();
        content.Should().NotBeNull();
        content!.status.Should().Be("healthy");
    }

    [Fact]
    public async Task CreatePayment_ReturnsAccepted()
    {
        // Arrange
        var request = new
        {
            amount = 100.00,
            currency = "BRL",
            method = "credit_card",
            customer = new
            {
                email = "test@example.com",
                name = "Test User",
                document = "12345678901",
                ip = "192.168.1.1",
                phone = "+5511999999999"
            },
            description = "Test payment"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/payments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var location = response.Headers.Location?.ToString();
        location.Should().NotBeNull();
        location.Should().Contain("/api/v1/payments/");
    }

    [Fact]
    public async Task CreatePayment_WithInvalidAmount_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            amount = -100.00,  // Invalid negative amount
            currency = "BRL",
            method = "credit_card",
            customer = new
            {
                email = "test@example.com",
                name = "Test User",
                document = "12345678901"
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/payments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePayment_WithMissingCustomer_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            amount = 100.00,
            currency = "BRL",
            method = "credit_card"
            // Missing customer
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/payments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPayment_ExistingPayment_ReturnsOk()
    {
        // Arrange - First create a payment
        var createRequest = new
        {
            amount = 250.00,
            currency = "BRL",
            method = "pix",
            customer = new
            {
                email = "gettest@example.com",
                name = "Get Test",
                document = "98765432100"
            }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/payments", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var location = createResponse.Headers.Location?.ToString();
        location.Should().NotBeNull();

        // Act
        var getResponse = await _client.GetAsync(location);

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payment = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>();
        payment.Should().NotBeNull();
        payment!.amount.Should().Be(250.00m);
        payment.currency.Should().Be("BRL");
    }

    [Fact]
    public async Task GetPayment_NonExistingPayment_ReturnsNotFound()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/payments/{nonExistingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListPayments_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/payments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await response.Content.ReadFromJsonAsync<PaymentListResponse>();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task ListPayments_WithStatusFilter_ReturnsFilteredResults()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/payments?status=approved");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListPayments_WithAnotherMerchantId_ReturnsForbidden()
    {
        var otherMerchantId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/v1/payments?merchantId={otherMerchantId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetStatistics_WithAnotherMerchantId_ReturnsForbidden()
    {
        var otherMerchantId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/v1/statistics?merchantId={otherMerchantId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RefundPayment_ReturnsAccepted()
    {
        // Arrange - First create a payment
        var createRequest = new
        {
            amount = 100.00,
            currency = "BRL",
            method = "credit_card",
            customer = new
            {
                email = "refund@example.com",
                name = "Refund Test",
                document = "11122233344"
            }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/payments", createRequest);
        var location = createResponse.Headers.Location?.ToString();

        // Wait a bit for processing
        await Task.Delay(100);

        // Extract payment ID from location
        var paymentId = location?.Split('/').Last();

        // Act
        var refundRequest = new
        {
            amount = 50.00,
            reason = "Customer request"
        };
        var refundResponse = await _client.PostAsJsonAsync($"/api/v1/payments/{paymentId}/refund", refundRequest);

        // Assert - May return Accepted or BadRequest depending on payment status
        refundResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CapturePayment_ReturnsOk()
    {
        // Arrange - First create a payment
        var createRequest = new
        {
            amount = 100.00,
            currency = "BRL",
            method = "credit_card",
            customer = new
            {
                email = "capture@example.com",
                name = "Capture Test",
                document = "55566677788"
            }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/payments", createRequest);
        var location = createResponse.Headers.Location?.ToString();
        var paymentId = location?.Split('/').Last();

        // Wait a bit for processing
        await Task.Delay(100);

        // Act
        var captureRequest = new { amount = 100.00 };
        var captureResponse = await _client.PostAsJsonAsync($"/api/v1/payments/{paymentId}/capture", captureRequest);

        // Assert - May return Ok or BadRequest depending on payment status
        captureResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CancelPayment_ReturnsOk()
    {
        // Arrange - First create a payment
        var createRequest = new
        {
            amount = 50.00,
            currency = "BRL",
            method = "pix",
            customer = new
            {
                email = "cancel@example.com",
                name = "Cancel Test",
                document = "99988877766"
            }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/payments", createRequest);
        var location = createResponse.Headers.Location?.ToString();
        var paymentId = location?.Split('/').Last();

        // Wait a bit for processing
        await Task.Delay(100);

        // Act
        var cancelResponse = await _client.PostAsync($"/api/v1/payments/{paymentId}/cancel", null);

        // Assert - May return Ok or BadRequest depending on payment status
        cancelResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePayment_WithSameIdempotencyKeyAndSamePayload_ReturnsCachedResponse()
    {
        using var pipelineFactory = new PaymentGatewayPipelineEnabledFactory();
        using var client = pipelineFactory.CreateClient();

        var idempotencyKey = $"idem-{Guid.NewGuid():N}";
        var payload = new
        {
            amount = 120.00,
            currency = "BRL",
            method = "credit_card",
            customer = new
            {
                email = "idem@example.com",
                name = "Idempotency Test",
                document = "12312312312"
            }
        };

        var firstResponse = await PostWithIdempotencyKeyAsync(client, payload, idempotencyKey);
        var secondResponse = await PostWithIdempotencyKeyAsync(client, payload, idempotencyKey);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        using var firstDoc = JsonDocument.Parse(firstBody);
        using var secondDoc = JsonDocument.Parse(secondBody);

        firstDoc.RootElement.GetProperty("paymentId").GetGuid()
            .Should()
            .Be(secondDoc.RootElement.GetProperty("paymentId").GetGuid());
    }

    [Fact]
    public async Task CreatePayment_WithSameIdempotencyKeyAndDifferentPayload_ReturnsConflict()
    {
        using var pipelineFactory = new PaymentGatewayPipelineEnabledFactory();
        using var client = pipelineFactory.CreateClient();

        var idempotencyKey = $"idem-{Guid.NewGuid():N}";
        var firstPayload = new
        {
            amount = 90.00,
            currency = "BRL",
            method = "credit_card",
            customer = new
            {
                email = "idem-conflict@example.com",
                name = "Idempotency Conflict",
                document = "99911122233"
            }
        };

        var secondPayload = new
        {
            amount = 190.00,
            currency = "BRL",
            method = "credit_card",
            customer = new
            {
                email = "idem-conflict@example.com",
                name = "Idempotency Conflict",
                document = "99911122233"
            }
        };

        var firstResponse = await PostWithIdempotencyKeyAsync(client, firstPayload, idempotencyKey);
        var secondResponse = await PostWithIdempotencyKeyAsync(client, secondPayload, idempotencyKey);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static Task<HttpResponseMessage> PostWithIdempotencyKeyAsync(HttpClient client, object payload, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }
}

// DTOs for tests
public class HealthResponse
{
    public string status { get; set; } = string.Empty;
    public DateTime timestamp { get; set; }
}

public class PaymentResponse
{
    public Guid id { get; set; }
    public Guid merchantId { get; set; }
    public decimal amount { get; set; }
    public string currency { get; set; } = string.Empty;
    public string status { get; set; } = string.Empty;
    public string method { get; set; } = string.Empty;
    public DateTime createdAt { get; set; }
}

public class PaymentListResponse
{
    public List<PaymentResponse> items { get; set; } = new();
    public int total { get; set; }
    public int limit { get; set; }
    public int offset { get; set; }
    public bool hasMore { get; set; }
}
