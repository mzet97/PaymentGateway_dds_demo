using PaymentGateway.Application.Common.Messaging;
using PaymentGateway.Application.UseCases.Merchants.ViewModels;

namespace PaymentGateway.Application.UseCases.Merchants.Commands;

public sealed class UpdateMerchantCommand : BrighterRequest<MerchantDto>
{
    public Guid MerchantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? CallbackUrl { get; set; }
}
