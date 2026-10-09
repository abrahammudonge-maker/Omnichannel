namespace Omni.Domain.ValueObjects;

/// <summary>
/// Canonical form for WhatsApp numbers. Meta's webhooks identify senders by bare international digits
/// ("254712345678"), while API callers and agents type "+254 712 345 678" or "00254...". Storing and
/// comparing the raw strings created duplicate customers, so a reply to a template landed in a new
/// conversation. Everything that stores or matches a WhatsApp number goes through here.
/// </summary>
public static class WhatsAppNumber
{
    private const string DefaultCountryCode = "254";

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        if (digits.Length == 0)
        {
            return value.Trim();
        }

        // Local Kenyan mobile formats: "0712345678" and "712345678" both mean 254712345678.
        if (digits.Length == 10 && digits[0] == '0')
        {
            return DefaultCountryCode + digits[1..];
        }

        if (digits.Length == 9 && digits[0] is '7' or '1')
        {
            return DefaultCountryCode + digits;
        }

        return digits;
    }

    /// <summary>
    /// Normalize for an outbound send: null unless the result is a plausible international number
    /// (8 to 15 digits, E.164's limit). Normalize itself stays lenient because it's also used for matching.
    /// </summary>
    public static string? NormalizeForSending(string? value)
    {
        var normalized = Normalize(value);
        return normalized is { Length: >= 8 and <= 15 } && normalized.All(char.IsAsciiDigit) ? normalized : null;
    }

    public static bool AreEqual(string? a, string? b)
    {
        var left = Normalize(a);
        return !string.IsNullOrEmpty(left) && string.Equals(left, Normalize(b), StringComparison.Ordinal);
    }
}
