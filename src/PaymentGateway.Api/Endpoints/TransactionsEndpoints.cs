using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Api.Common.Authorization;
using PaymentGateway.Infrastructure.Persistence.Postgres;

namespace PaymentGateway.Api.Endpoints;

public static class TransactionsEndpoints
{
    public static WebApplication MapTransactionsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/transactions", async (
            string? merchantId,
            string? paymentId,
            string? type,
            int? limit,
            int? offset,
            ClaimsPrincipal user,
            IMerchantScopeService merchantScopeService,
            PaymentDbContext dbContext,
            CancellationToken ct) =>
        {
            var requestedMerchantId = Guid.TryParse(merchantId, out var mid) ? mid : (Guid?)null;
            var scope = merchantScopeService.Resolve(user, requestedMerchantId, allowGlobalForAdmin: true);
            if (scope.ToErrorResult() is { } error)
                return error;

            var query = dbContext.Transactions.AsNoTracking().AsQueryable();

            if (Guid.TryParse(paymentId, out var pid))
                query = query.Where(t => t.PaymentId == pid);

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(t => t.Type == type);

            // Filter by merchant via payment join
            if (scope.MerchantId.HasValue && scope.MerchantId.Value != Guid.Empty)
            {
                var merchantPaymentIds = dbContext.Payments
                    .AsNoTracking()
                    .Where(p => p.MerchantId == scope.MerchantId.Value)
                    .Select(p => p.Id);
                query = query.Where(t => merchantPaymentIds.Contains(t.PaymentId));
            }

            var total = await query.CountAsync(ct);
            var take = Math.Clamp(limit ?? 50, 1, 100);
            var skip = Math.Max(offset ?? 0, 0);

            var transactions = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip(skip)
                .Take(take)
                .Select(t => new
                {
                    t.Id,
                    t.PaymentId,
                    t.Type,
                    t.Amount,
                    t.Currency,
                    t.Reference,
                    t.Reason,
                    t.Success,
                    t.ErrorCode,
                    t.CreatedAt,
                })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                items = transactions,
                total,
                limit = take,
                offset = skip,
                hasMore = skip + take < total
            });
        })
        .WithTags("Transactions")
        .WithName("GetTransactions")
        .RequireAuthorization();

        return app;
    }
}
