using System.Text.Json;

namespace PaymentGateway.Infrastructure.Observability;

public static class SensitiveDataSanitizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static object? Sanitize(object? payload)
    {
        if (payload == null)
            return null;

        try
        {
            var element = JsonSerializer.SerializeToElement(payload, JsonOptions);
            return SanitizeElement(element, null);
        }
        catch
        {
            return payload.ToString();
        }
    }

    public static string MaskEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var parts = value.Split('@', 2);
        if (parts.Length != 2)
            return "***";

        var local = parts[0];
        var maskedLocal = local.Length switch
        {
            <= 1 => "*",
            2 => $"{local[0]}*",
            _ => $"{local[0]}***{local[^1]}"
        };

        return $"{maskedLocal}@{parts[1]}";
    }

    public static string MaskDocument(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (trimmed.Length <= 4)
            return "***";

        return $"{trimmed[..2]}***{trimmed[^2..]}";
    }

    public static string MaskIp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var segments = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 4)
            return $"{segments[0]}.{segments[1]}.*.*";

        return "***";
    }

    public static string MaskPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
            return "***";

        return $"***{digits[^4..]}";
    }

    private static object? SanitizeElement(JsonElement element, string? propertyName)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => SanitizeObject(element),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(item => SanitizeElement(item, propertyName))
                .ToList(),
            JsonValueKind.String => SanitizeString(element.GetString(), propertyName),
            JsonValueKind.Number => element.TryGetDecimal(out var decimalValue)
                ? decimalValue
                : element.ToString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => element.ToString()
        };
    }

    private static Dictionary<string, object?> SanitizeObject(JsonElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = SanitizeElement(property.Value, property.Name);
        }

        return result;
    }

    private static object? SanitizeString(string? value, string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (propertyName == null)
            return value;

        return NormalizeKey(propertyName) switch
        {
            "email" => MaskEmail(value),
            "document" => MaskDocument(value),
            "customerdocument" => MaskDocument(value),
            "ip" => MaskIp(value),
            "customerip" => MaskIp(value),
            "phone" => MaskPhone(value),
            "customerphone" => MaskPhone(value),
            "secret" or "password" or "apikey" or "accesskey" or "token" or "signature"
                => "***",
            _ => value
        };
    }

    private static string NormalizeKey(string propertyName)
    {
        return new string(propertyName
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}
