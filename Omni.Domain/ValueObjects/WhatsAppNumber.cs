namespace Omni.Domain.ValueObjects;

/// <summary>
/// Canonical form for WhatsApp numbers. Meta's webhooks identify senders by bare international digits
/// ("254712345678"), while API callers and agents type "+254 712 345 678" or "00254...". Storing and
/// comparing the raw strings created duplicate customers, so a reply to a template landed in a new
/// conversation. Everything that stores or matches a WhatsApp number goes through here.
/// </summary>
public static class WhatsAppNumber
{
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

        return digits.Length == 0 ? value.Trim() : digits;
    }

    public static bool AreEqual(string? a, string? b)
    {
        var left = Normalize(a);
        return !string.IsNullOrEmpty(left) && string.Equals(left, Normalize(b), StringComparison.Ordinal);
    }
}
