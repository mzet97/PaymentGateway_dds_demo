using Paramore.Darker;
using PaymentGateway.Application.UseCases.Statistics.ViewModels;

namespace PaymentGateway.Application.UseCases.Statistics.Queries;

public sealed class GetStatisticsQuery : IQuery<StatisticsDto>
{
    public Guid? MerchantId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? GroupBy { get; init; }
}
