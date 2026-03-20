using FluentAssertions;
using PaymentGateway.Infrastructure.DDS;
using Xunit;

namespace PaymentGateway.IntegrationTests;

public class InMemoryDdsPublisherTests
{
    [Fact]
    public async Task DequeueMessages_ShouldRemoveMessagesAfterRead()
    {
        var publisher = new InMemoryDdsPublisher(logToConsole: false);

        await publisher.PublishAsync("payment.create", new { id = 1 });
        await publisher.PublishAsync("payment.create", new { id = 2 });

        var firstBatch = publisher.DequeueMessages("payment.create");
        var secondBatch = publisher.DequeueMessages("payment.create");

        firstBatch.Should().HaveCount(2);
        secondBatch.Should().BeEmpty();
    }
}
