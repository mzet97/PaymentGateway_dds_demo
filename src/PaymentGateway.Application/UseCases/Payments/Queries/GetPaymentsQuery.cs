using Paramore.Darker;
using PaymentGateway.Application.UseCases.Payments.ViewModels;

namespace PaymentGateway.Application.UseCases.Payments.Queries;

public sealed class GetPaymentsQuery : IQuery<PaymentsListResult>
{
    public Guid? MerchantId { get; set; }
    public string? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Limit { get; set; } = 50;
    public int Offset { get; set; }
    public string? SortBy { get; set; }
    public string? SortOrder { get; set; }
}
