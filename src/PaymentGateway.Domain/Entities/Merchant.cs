namespace PaymentGateway.Domain.Entities;

public class Merchant : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Document { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public MerchantStatus Status { get; private set; }
    public string? CallbackUrl { get; private set; }
    public string? ApiKey { get; private set; }
    public decimal MaxTransactionAmount { get; private set; } = 10000m;
    public decimal MaxDailyAmount { get; private set; } = 100000m;
    public int MaxDailyTransactions { get; private set; } = 1000;
    public DateTime? VerifiedAt { get; private set; }
    public DateTime? SuspendedAt { get; private set; }

    private Merchant() : base() { }

    public static Merchant Create(
        string name,
        string email,
        string document,
        string category,
        string? callbackUrl = null)
    {
        var merchant = new Merchant
        {
            Name = name,
            Email = email,
            Document = NormalizeDocument(document),
            Category = category,
            Status = MerchantStatus.PendingVerification,
            CallbackUrl = callbackUrl,
            ApiKey = GenerateApiKey()
        };

        return merchant;
    }

    public void Activate()
    {
        if (Status != MerchantStatus.PendingVerification)
            throw new InvalidOperationException("Only pending merchants can be activated");

        Status = MerchantStatus.Active;
        VerifiedAt = DateTime.UtcNow;
    }

    public void Suspend(string reason)
    {
        if (Status != MerchantStatus.Active)
            throw new InvalidOperationException("Only active merchants can be suspended");

        Status = MerchantStatus.Suspended;
        SuspendedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        if (Status != MerchantStatus.Suspended)
            throw new InvalidOperationException("Only suspended merchants can be reactivated");

        Status = MerchantStatus.Active;
        SuspendedAt = null;
    }

    public void Deactivate()
    {
        Status = MerchantStatus.Inactive;
    }

    public void UpdateLimits(decimal maxTransaction, decimal maxDailyAmount, int maxDailyTransactions)
    {
        MaxTransactionAmount = maxTransaction;
        MaxDailyAmount = maxDailyAmount;
        MaxDailyTransactions = maxDailyTransactions;
    }

    public bool CanProcessTransaction(decimal amount)
    {
        return Status == MerchantStatus.Active &&
               amount <= MaxTransactionAmount;
    }

    public void UpdateProfile(string name, string email, string category, string? callbackUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));

        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category is required", nameof(category));

        Name = name;
        Email = email;
        Category = category;
        CallbackUrl = callbackUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public static Merchant Rehydrate(
        Guid id,
        string name,
        string email,
        string document,
        string category,
        MerchantStatus status,
        string? callbackUrl,
        string? apiKey,
        decimal maxTransactionAmount,
        decimal maxDailyAmount,
        int maxDailyTransactions,
        DateTime createdAt,
        DateTime? verifiedAt,
        DateTime? suspendedAt = null)
    {
        return new Merchant
        {
            Id = id,
            Name = name,
            Email = email,
            Document = NormalizeDocument(document),
            Category = category,
            Status = status,
            CallbackUrl = callbackUrl,
            ApiKey = apiKey,
            MaxTransactionAmount = maxTransactionAmount,
            MaxDailyAmount = maxDailyAmount,
            MaxDailyTransactions = maxDailyTransactions,
            CreatedAt = createdAt,
            VerifiedAt = verifiedAt,
            SuspendedAt = suspendedAt
        };
    }

    private static string NormalizeDocument(string document)
    {
        return System.Text.RegularExpressions.Regex.Replace(document, @"[^\d]", "");
    }

    private static string GenerateApiKey()
    {
        return $"pk_{Guid.NewGuid():N}{Guid.NewGuid():N}";
    }
}
