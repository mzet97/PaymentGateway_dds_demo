namespace PaymentGateway.Application.UseCases.Payments.ViewModels;

public record CustomerDto
{
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Document { get; init; }
    public string? Ip { get; init; }
    public string? Phone { get; init; }
}
