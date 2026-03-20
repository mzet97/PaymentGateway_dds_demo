namespace PaymentGateway.Domain.Entities;

/// <summary>
/// Represents a webhook configuration for receiving payment notifications.
/// </summary>
public class Webhook : Entity
{
    public Guid MerchantId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public List<string> Events { get; private set; } = new();
    public string Secret { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    // For EF Core
    private Webhook() { }

    public static Webhook Create(Guid merchantId, string url, List<string> events, string secret, bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Webhook URL is required", nameof(url));

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            throw new ArgumentException("Invalid webhook URL", nameof(url));

        if (events == null || events.Count == 0)
            throw new ArgumentException("At least one event is required", nameof(events));

        return new Webhook
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Url = url,
            Events = events,
            Secret = secret,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Webhook Rehydrate(
        Guid id,
        Guid merchantId,
        string url,
        List<string> events,
        string secret,
        bool isActive,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Webhook ID is required", nameof(id));

        if (merchantId == Guid.Empty)
            throw new ArgumentException("Merchant ID is required", nameof(merchantId));

        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Webhook URL is required", nameof(url));

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            throw new ArgumentException("Invalid webhook URL", nameof(url));

        if (events == null || events.Count == 0)
            throw new ArgumentException("At least one event is required", nameof(events));

        return new Webhook
        {
            Id = id,
            MerchantId = merchantId,
            Url = url,
            Events = events,
            Secret = secret,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void UpdateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Webhook URL is required", nameof(url));

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            throw new ArgumentException("Invalid webhook URL", nameof(url));

        Url = url;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateEvents(List<string> events)
    {
        if (events == null || events.Count == 0)
            throw new ArgumentException("At least one event is required", nameof(events));

        Events = events;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool SupportsEvent(string eventType)
    {
        return Events.Contains(eventType);
    }
}
