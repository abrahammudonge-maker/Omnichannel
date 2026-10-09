using System.Text.RegularExpressions;
using Omni.Domain.Entities;

namespace Omni.Application.Services;

/// <summary>Checks done before a template send is queued, so bad input fails in the API response rather than later at Meta.</summary>
public static class WhatsAppTemplateRules
{
    public const int MaxParameterLength = 1024;
    private static readonly Regex BodyParameterPattern = new(@"\{\{\d+\}\}");
    private static readonly Regex TooManySpaces = new(@" {5,}");

    /// <summary>Approved, and not AUTHENTICATION (codes go through /api/otp so they keep per-number limits and redaction).</summary>
    public static bool IsSendable(MessageTemplate template) =>
        string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase) && !IsAuthentication(template);

    public static bool IsAuthentication(MessageTemplate template) =>
        string.Equals(template.Category, "AUTHENTICATION", StringComparison.OrdinalIgnoreCase);

    public static int CountBodyParameters(string? bodyText) =>
        string.IsNullOrEmpty(bodyText) ? 0 : BodyParameterPattern.Matches(bodyText).Count;

    /// <summary>Returns why Meta would reject these values, or null if they're fine.</summary>
    public static string? ValidateParameters(IReadOnlyList<string?> values, int expectedCount)
    {
        if (values.Count != expectedCount)
        {
            return $"This template needs {expectedCount} body parameter(s), got {values.Count}.";
        }

        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            var label = $"Body parameter {i + 1}";
            if (string.IsNullOrWhiteSpace(value)) return $"{label} is empty.";
            if (value.Contains('\n') || value.Contains('\r') || value.Contains('\t')) return $"{label} can't contain new lines or tabs.";
            if (TooManySpaces.IsMatch(value)) return $"{label} can't contain more than four spaces in a row.";
            if (value.Length > MaxParameterLength) return $"{label} is longer than {MaxParameterLength} characters.";
        }
        return null;
    }
}
