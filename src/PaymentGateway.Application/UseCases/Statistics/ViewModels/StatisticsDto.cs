namespace PaymentGateway.Application.UseCases.Statistics.ViewModels;

public sealed class StatisticsDto
{
    public int TotalTransactions { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal AverageAmount { get; init; }
    public int ApprovedCount { get; init; }
    public int RejectedCount { get; init; }
    public int RefundedCount { get; init; }
    public IReadOnlyDictionary<string, int> ByStatus { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> ByMethod { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<StatisticsBucketDto>? GroupedData { get; init; }
}
