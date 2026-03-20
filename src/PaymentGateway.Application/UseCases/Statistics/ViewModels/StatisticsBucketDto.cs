namespace PaymentGateway.Application.UseCases.Statistics.ViewModels;

public sealed class StatisticsBucketDto
{
    public string Period { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal Amount { get; init; }
}
