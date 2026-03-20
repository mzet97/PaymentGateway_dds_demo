namespace PaymentGateway.Application.UseCases.Payments.ViewModels;

public record PaymentsListResult
{
    public List<PaymentDto> Items { get; init; } = [];
    public int Total { get; init; }
    public int Limit { get; init; }
    public int Offset { get; init; }
    public bool HasMore { get; init; }
}
