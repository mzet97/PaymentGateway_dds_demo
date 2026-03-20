using Paramore.Brighter;

namespace PaymentGateway.Application.Common.Messaging;

public static class CommandProcessorExtensions
{
    public static async Task<TResult> SendWithResultAsync<TRequest, TResult>(
        this IAmACommandProcessor commandProcessor,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : BrighterRequest<TResult>
    {
        ArgumentNullException.ThrowIfNull(commandProcessor);
        ArgumentNullException.ThrowIfNull(request);

        BrighterLoggingBootstrap.EnsureInitialized();
        await commandProcessor.SendAsync(request, cancellationToken: cancellationToken);
        return request.Result!;
    }
}
