using System.Net;
using System.Text.Json;

namespace Omni.Infrastructure.Providers;

/// <summary>
/// A Graph API call Meta rejected. Carries the HTTP status and Meta's error code so callers can tell a
/// temporary failure (throttling, Meta outage) worth retrying from one that will never succeed.
/// </summary>
public sealed class MetaApiException : InvalidOperationException
{
    // Meta error codes that clear up on their own: generic/unknown errors, throttling, and service outages.
    // https://developers.facebook.com/docs/whatsapp/cloud-api/support/error-codes
    private static readonly HashSet<int> TransientCodes = new()
    {
        1, 2, 4, 17, 341, 80007, 130429, 131000, 131016, 131048, 131056, 133004
    };

    public MetaApiException(string message, HttpStatusCode statusCode, string responseBody) : base(message)
    {
        StatusCode = statusCode;
        (ErrorCode, Details) = ParseError(responseBody);
    }

    public HttpStatusCode StatusCode { get; }
    public int? ErrorCode { get; }

    /// <summary>Meta's human-readable reason, e.g. "Recipient phone number not in allowed list".</summary>
    public string? Details { get; }

    public bool IsTransient =>
        (int)StatusCode >= 500 || StatusCode == HttpStatusCode.TooManyRequests || (ErrorCode is int code && TransientCodes.Contains(code));

    /// <summary>True for failures that are likely to succeed if the same request is retried later.</summary>
    public static bool IsTransientFailure(Exception ex) => ex switch
    {
        MetaApiException meta => meta.IsTransient,
        HttpRequestException => true,
        TaskCanceledException => true, // HttpClient timeout
        _ => false
    };

    private static (int? Code, string? Details) ParseError(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("error", out var error))
            {
                return (null, null);
            }

            int? code = error.TryGetProperty("code", out var codeProp) && codeProp.TryGetInt32(out var c) ? c : null;
            var details = error.TryGetProperty("error_data", out var data) && data.TryGetProperty("details", out var detailsProp)
                ? detailsProp.GetString()
                : null;
            details ??= error.TryGetProperty("message", out var messageProp) ? messageProp.GetString() : null;
            return (code, details);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
