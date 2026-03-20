using Paramore.Brighter;

namespace PaymentGateway.Application.Common.Messaging;

public abstract class BrighterRequest<TResult> : IRequest
{
    public TResult? Result { get; set; }

    public Id Id { get; set; } = new(Guid.NewGuid().ToString());

    public Id? CorrelationId { get; set; } = new(Guid.NewGuid().ToString());
}
