using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Logging;

namespace PaymentGateway.Application.Common.Messaging;

internal static class BrighterLoggingBootstrap
{
    private static readonly ILoggerFactory StableLoggerFactory = NullLoggerFactory.Instance;

    public static void EnsureInitialized()
    {
        ApplicationLogging.LoggerFactory = StableLoggerFactory;
    }
}
