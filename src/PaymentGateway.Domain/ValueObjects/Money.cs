namespace PaymentGateway.Domain.ValueObjects;

public readonly record struct Money : IComparable<Money>, IComparable
{
    public decimal Amount { get; }
    public string Currency { get; }

    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BRL", "USD", "EUR", "GBP"
    };

    public static readonly Money Zero = new(0, "BRL");

    public Money(decimal amount, string currency = "BRL")
    {
        if (amount < 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required", nameof(currency));

        if (!SupportedCurrencies.Contains(currency))
            throw new ArgumentException($"Currency '{currency}' is not supported", nameof(currency));

        Amount = Math.Round(amount, 2);
        Currency = currency.ToUpperInvariant();
    }

    public static bool IsSupportedCurrency(string currency) =>
        SupportedCurrencies.Contains(currency);

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add money with different currencies");

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot subtract money with different currencies");

        var result = Amount - other.Amount;
        if (result < 0)
            throw new InvalidOperationException("Result cannot be negative");

        return new Money(result, Currency);
    }

    public bool IsZero => Amount == 0;
    public bool IsPositive => Amount > 0;

    public static bool operator >(Money left, Money right) => left.Amount > right.Amount;
    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;
    public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;
    public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;

    public int CompareTo(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot compare money with different currencies");
        return Amount.CompareTo(other.Amount);
    }

    public int CompareTo(object? obj)
    {
        if (obj is Money other)
            return CompareTo(other);
        throw new ArgumentException("Object is not a Money", nameof(obj));
    }

    public override string ToString() => $"{Amount:N2} {Currency}";

    public static implicit operator decimal(Money money) => money.Amount;
}
