using FluentAssertions;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.ValueObjects;
using Xunit;

namespace PaymentGateway.IntegrationTests;

public class PaymentDomainRulesTests
{
    [Fact]
    public void Capture_WithPartialAmount_ShouldThrow()
    {
        var payment = CreateApprovedPayment();

        var act = () => payment.Capture(50m);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Partial capture is not supported*");
    }

    [Fact]
    public void Capture_WithFullAmount_ShouldSetCapturedStatus()
    {
        var payment = CreateApprovedPayment();

        payment.Capture(100m);

        payment.Status.Should().Be(PaymentStatus.Captured);
        payment.CapturedAt.Should().NotBeNull();
    }

    private static Payment CreateApprovedPayment()
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            new Money(100m, "BRL"),
            PaymentMethod.CreditCard,
            new CustomerInfo("test@example.com", "Test User", "12345678901"));

        payment.Approve("tx-1");
        return payment;
    }
}
