namespace PaymentGateway.Application.UseCases.Payments.ViewModels;

public record PaymentItemDto
{
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
