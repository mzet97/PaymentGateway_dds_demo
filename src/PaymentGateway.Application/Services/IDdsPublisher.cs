namespace PaymentGateway.Application.Services;

public interface IDdsPublisher
{
    Task PublishAsync<T>(string topic, T data, CancellationToken ct = default) where T : class;
}
