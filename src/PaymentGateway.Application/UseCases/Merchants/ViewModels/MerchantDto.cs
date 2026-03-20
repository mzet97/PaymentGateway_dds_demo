using PaymentGateway.Domain.Entities;

namespace PaymentGateway.Application.UseCases.Merchants.ViewModels;

public sealed class MerchantDto
{
    public Guid MerchantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Document { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public MerchantStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CallbackUrl { get; init; }
}
