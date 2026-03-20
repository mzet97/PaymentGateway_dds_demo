using PaymentGateway.Application.Common.Messaging;
using PaymentGateway.Application.UseCases.Merchants.ViewModels;

namespace PaymentGateway.Application.UseCases.Merchants.Commands;

public sealed class CreateMerchantCommand : BrighterRequest<CreateMerchantResult>
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Document { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? CallbackUrl { get; set; }
}
