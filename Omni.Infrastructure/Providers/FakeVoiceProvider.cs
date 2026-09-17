using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Omni.Application.Interfaces;
using Omni.Domain.Constants;

namespace Omni.Infrastructure.Providers;

/// <summary>
/// In-memory stand-in for a real telephony provider — never makes a real call or network request.
/// Used for local development and automated tests so the voice engine can be built and exercised
/// end-to-end before a paid provider is wired up. Its webhook payload shape and signature scheme
/// (HMAC-SHA256 over the raw body, hex-encoded in an X-Fake-Signature header) are its own —
/// a real provider adapter (e.g. Twilio) will follow that provider's own documented scheme instead.
/// </summary>
public sealed class FakeVoiceProvider : IVoiceProvider
{
    public const string SignatureHeaderName = "X-Fake-Signature";
    private const string SigningSecret = "fake-voice-provider-test-secret";

    public string Name => VoiceProviderName.Fake;

    public Task<CallResult> InitiateCallAsync(InitiateCallRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Ok($"fake-{Guid.NewGuid():N}", CallStatus.Ringing));

    public Task<CallResult> AnswerCallAsync(string providerCallId, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Ok(providerCallId, CallStatus.Answered));

    public Task<CallResult> HangupCallAsync(string providerCallId, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Ok(providerCallId, CallStatus.Completed));

    public Task<CallResult> HoldCallAsync(string providerCallId, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Ok(providerCallId, CallStatus.OnHold));

    public Task<CallResult> ResumeCallAsync(string providerCallId, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Ok(providerCallId, CallStatus.Answered));

    public Task<CallResult> TransferCallAsync(string providerCallId, string destination, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Ok(providerCallId, CallStatus.Transferred));

    public Task<CallResult> StartRecordingAsync(string providerCallId, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Ok(providerCallId));

    public Task<CallResult> StopRecordingAsync(string providerCallId, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Ok(providerCallId));

    public Task<bool> ValidateWebhookAsync(WebhookValidationContext context, CancellationToken cancellationToken)
    {
        if (!context.Headers.TryGetValue(SignatureHeaderName, out var provided) || string.IsNullOrWhiteSpace(provided))
        {
            return Task.FromResult(false);
        }

        var expected = Sign(context.RawBody);
        var isValid = CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expected),
            TryDecodeHex(provided) ?? []);
        return Task.FromResult(isValid);
    }

    public Task<VoiceWebhookEvent?> ProcessWebhookAsync(string payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return Task.FromResult<VoiceWebhookEvent?>(null);
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (!root.TryGetProperty("providerCallId", out var providerCallIdProp) || !root.TryGetProperty("eventType", out var eventTypeProp))
        {
            return Task.FromResult<VoiceWebhookEvent?>(null);
        }

        var webhookEvent = new VoiceWebhookEvent(
            ProviderCallId: providerCallIdProp.GetString()!,
            EventType: eventTypeProp.GetString()!,
            Status: root.TryGetProperty("status", out var s) ? s.GetString() : null,
            Timestamp: root.TryGetProperty("timestamp", out var t) && t.TryGetDateTimeOffset(out var ts) ? ts : null,
            RecordingUrl: root.TryGetProperty("recordingUrl", out var ru) ? ru.GetString() : null,
            RecordingStatus: root.TryGetProperty("recordingStatus", out var rs) ? rs.GetString() : null,
            DurationSeconds: root.TryGetProperty("durationSeconds", out var d) && d.TryGetInt32(out var dur) ? dur : null,
            FromNumber: root.TryGetProperty("fromNumber", out var fn) ? fn.GetString() : null,
            ToNumber: root.TryGetProperty("toNumber", out var tn) ? tn.GetString() : null,
            Direction: root.TryGetProperty("direction", out var dir) ? dir.GetString() : null,
            ProviderEventId: root.TryGetProperty("providerEventId", out var pe) ? pe.GetString() : null);

        return Task.FromResult<VoiceWebhookEvent?>(webhookEvent);
    }

    /// <summary>Exposed for tests that need to construct a validly-signed fake webhook request.</summary>
    public static string Sign(string rawBody) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(SigningSecret), Encoding.UTF8.GetBytes(rawBody)));

    private static byte[]? TryDecodeHex(string value)
    {
        try
        {
            return Convert.FromHexString(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
