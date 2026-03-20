using Microsoft.Extensions.Logging;
using Paramore.Brighter;

namespace PaymentGateway.Application.Common.Behaviours;

public sealed class RequestLoggingAttribute : RequestHandlerAttribute
{
    public RequestLoggingAttribute(int step, HandlerTiming timing = HandlerTiming.Before)
        : base(step, timing)
    {
    }

    public override Type GetHandlerType()
    {
        return typeof(UnhandledExceptionHandler<>);
    }
}

public sealed class UnhandledExceptionHandler<TRequest> : RequestHandlerAsync<TRequest>
    where TRequest : class, IRequest
{
    private readonly ILogger<TRequest> _logger;

    public UnhandledExceptionHandler(ILogger<TRequest> logger)
    {
        _logger = logger;
    }

    public override async Task<TRequest> HandleAsync(TRequest command, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.HandleAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Request: Unhandled Exception for Request {RequestName} RequestId {RequestId} CorrelationId {CorrelationId}",
                typeof(TRequest).Name,
                command.Id,
                command.CorrelationId);
            throw;
        }
    }
}
