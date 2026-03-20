using System.Security.Claims;
using Paramore.Darker;
using PaymentGateway.Api.Common.Authorization;
using PaymentGateway.Application.UseCases.Statistics.Queries;

namespace PaymentGateway.Api.Endpoints;

public static class StatisticsEndpoints
{
    public static WebApplication MapStatisticsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/statistics", async (
            Guid? merchantId,
            DateTime? from,
            DateTime? to,
            string? groupBy,
            ClaimsPrincipal user,
            IMerchantScopeService merchantScopeService,
            IQueryProcessor queryProcessor,
            CancellationToken ct) =>
        {
            var scope = merchantScopeService.Resolve(user, merchantId, allowGlobalForAdmin: true);
            if (scope.ToErrorResult() is { } error)
                return error;

            var result = await queryProcessor.ExecuteAsync(new GetStatisticsQuery
            {
                MerchantId = scope.MerchantId,
                From = from,
                To = to,
                GroupBy = groupBy
            }, ct);

            return Results.Ok(result);
        })
        .WithTags("Statistics")
        .WithName("GetStatistics")
        .RequireAuthorization();

        return app;
    }
}
