using PaymentGateway.Application.UseCases.Statistics.Queries;
using PaymentGateway.Application.UseCases.Statistics.ViewModels;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.Repositories;
using Paramore.Darker;

namespace PaymentGateway.Application.UseCases.Statistics.Queries.Handlers;

public sealed class GetStatisticsQueryHandler : QueryHandlerAsync<GetStatisticsQuery, StatisticsDto>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetStatisticsQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public override async Task<StatisticsDto> ExecuteAsync(GetStatisticsQuery query, CancellationToken cancellationToken = default)
    {
        var payments = (await _paymentRepository.GetByMerchantAsync(
            query.MerchantId ?? Guid.Empty,
            null,
            query.From,
            query.To,
            10000,
            0,
            cancellationToken)).ToList();

        var totalTransactions = payments.Count;
        var totalAmount = payments.Sum(payment => payment.Amount.Amount);
        var averageAmount = totalTransactions > 0 ? totalAmount / totalTransactions : 0m;

        var byStatus = payments
            .GroupBy(payment => payment.Status.ToString().ToLowerInvariant())
            .ToDictionary(group => group.Key, group => group.Count());

        var byMethod = payments
            .GroupBy(payment => payment.Method.ToString().ToLowerInvariant())
            .ToDictionary(group => group.Key, group => group.Count());

        return new StatisticsDto
        {
            TotalTransactions = totalTransactions,
            TotalAmount = totalAmount,
            AverageAmount = averageAmount,
            ApprovedCount = payments.Count(payment => payment.Status == PaymentStatus.Approved),
            RejectedCount = payments.Count(payment => payment.Status == PaymentStatus.Rejected),
            RefundedCount = payments.Count(payment => payment.Status == PaymentStatus.Refunded),
            ByStatus = byStatus,
            ByMethod = byMethod,
            GroupedData = BuildGroupedData(payments, query.GroupBy)
        };
    }

    private static IReadOnlyList<StatisticsBucketDto>? BuildGroupedData(IEnumerable<Domain.Entities.Payment> payments, string? groupBy)
    {
        if (string.IsNullOrWhiteSpace(groupBy))
        {
            return null;
        }

        return groupBy.ToLowerInvariant() switch
        {
            "hour" => payments
                .GroupBy(payment => new { payment.CreatedAt.Year, payment.CreatedAt.Month, payment.CreatedAt.Day, payment.CreatedAt.Hour })
                .Select(group => new StatisticsBucketDto
                {
                    Period = $"{group.Key.Year}-{group.Key.Month:D2}-{group.Key.Day:D2} {group.Key.Hour}:00",
                    Count = group.Count(),
                    Amount = group.Sum(payment => payment.Amount.Amount)
                })
                .ToList(),
            "day" => payments
                .GroupBy(payment => new { payment.CreatedAt.Year, payment.CreatedAt.Month, payment.CreatedAt.Day })
                .Select(group => new StatisticsBucketDto
                {
                    Period = $"{group.Key.Year}-{group.Key.Month:D2}-{group.Key.Day:D2}",
                    Count = group.Count(),
                    Amount = group.Sum(payment => payment.Amount.Amount)
                })
                .ToList(),
            "month" => payments
                .GroupBy(payment => new { payment.CreatedAt.Year, payment.CreatedAt.Month })
                .Select(group => new StatisticsBucketDto
                {
                    Period = $"{group.Key.Year}-{group.Key.Month:D2}",
                    Count = group.Count(),
                    Amount = group.Sum(payment => payment.Amount.Amount)
                })
                .ToList(),
            _ => null
        };
    }
}
