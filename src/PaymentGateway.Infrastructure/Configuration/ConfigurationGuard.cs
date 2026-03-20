namespace PaymentGateway.Infrastructure.Configuration;

public static class ConfigurationGuard
{
    public static string Require(string? value, string configurationPath)
    {
        if (IsMissing(value))
        {
            throw new InvalidOperationException(
                $"Configuration '{configurationPath}' is required. Set '{configurationPath.Replace(":", "__", StringComparison.Ordinal)}' environment variable.");
        }

        return value!;
    }

    private static bool IsMissing(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return value.StartsWith("__", StringComparison.Ordinal)
               && value.EndsWith("__", StringComparison.Ordinal);
    }
}
