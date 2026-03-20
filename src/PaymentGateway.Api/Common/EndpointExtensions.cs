using PaymentGateway.Api.Common;

namespace PaymentGateway.Api.Extensions;

public static class EndpointExtensions
{
    public static void MapEndpoints(this IEndpointRouteBuilder app)
    {
        var endpointTypes = typeof(Program).Assembly
            .GetTypes()
            .Where(t => typeof(IEndpoint).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var endpointType in endpointTypes)
        {
            var mapMethod = typeof(IEndpoint).GetMethod("Map");
            var endpointInstance = Activator.CreateInstance(endpointType);
            mapMethod?.Invoke(endpointInstance, new object[] { app });
        }
    }
}
