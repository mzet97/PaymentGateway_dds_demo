namespace PaymentGateway.Api.Middlewares;

public class IdempotencyResponse
{
    public int StatusCode { get; set; }
    public string ContentType { get; set; } = "application/json";
    public string Body { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
}
