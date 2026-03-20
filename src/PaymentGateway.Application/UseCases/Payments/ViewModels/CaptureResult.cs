namespace PaymentGateway.Application.UseCases.Payments.ViewModels;

public record CaptureResult
{
    public Guid PaymentId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CapturedAt { get; init; }
}
