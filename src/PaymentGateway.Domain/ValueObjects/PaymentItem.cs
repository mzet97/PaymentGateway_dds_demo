namespace PaymentGateway.Domain.ValueObjects;

public sealed record PaymentItem
{
    public string Sku { get; }
    public string Name { get; }
    public int Quantity { get; }
    public Money UnitPrice { get; }

    public PaymentItem(
        string sku,
        string name,
        int quantity,
        decimal unitPrice,
        string currency = "BRL")
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU is required", nameof(sku));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));

        Sku = sku;
        Name = name;
        Quantity = quantity;
        UnitPrice = new Money(unitPrice, currency);
    }

    public Money TotalPrice => new(UnitPrice.Amount * Quantity, UnitPrice.Currency);
}
