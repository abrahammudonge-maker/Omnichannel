namespace Omni.Application.Interfaces;

public sealed record InitiateCallRequest(
    Guid OrganizationId,
    string FromNumber,
    string ToNumber,
    string? ProviderNumberId,
    string? StatusCallbackUrl);

public sealed record CallResult(bool Success, string? ProviderCallId, string? Status, string? ErrorMessage)
{
    public static CallResult Ok(string? providerCallId = null, string? status = null) => new(true, providerCallId, status, null);
    public static CallResult Fail(string errorMessage) => new(false, null, null, errorMessage);
}

/// <summary>
/// Everything a provider needs to verify a webhook actually came from it — deliberately not the raw
/// ASP.NET Core HttpRequest, so providers (and their unit tests) stay free of any web-framework
/// dependency and can be constructed/tested with a plain object.
/// </summary>
public sealed record WebhookValidationContext(string RawBody, string RequestUrl, IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// Normalized shape every provider's webhook payload gets translated into, regardless of that
/// provider's own wire format. FromNumber/ToNumber/Direction are only populated for a call the
/// provider hasn't reported before (a fresh inbound call) — IVoiceService uses them to identify the
/// organization by ToNumber and create the Call row; for a known ProviderCallId they're ignored.
/// </summary>
public sealed record VoiceWebhookEvent(
    string ProviderCallId,
    string EventType,
    string? Status,
    DateTimeOffset? Timestamp,
    string? RecordingUrl,
    string? RecordingStatus,
    int? DurationSeconds,
    string? FromNumber = null,
    string? ToNumber = null,
    string? Direction = null,
    string? ProviderEventId = null);

public interface IVoiceProvider
{
    /// <summary>Matches one of the <see cref="Omni.Domain.Constants.VoiceProviderName"/> values — how <see cref="IVoiceProviderFactory"/> resolves this provider.</summary>
    string Name { get; }

    Task<CallResult> InitiateCallAsync(InitiateCallRequest request, CancellationToken cancellationToken);
    Task<CallResult> AnswerCallAsync(string providerCallId, CancellationToken cancellationToken);
    Task<CallResult> HangupCallAsync(string providerCallId, CancellationToken cancellationToken);
    Task<CallResult> HoldCallAsync(string providerCallId, CancellationToken cancellationToken);
    Task<CallResult> ResumeCallAsync(string providerCallId, CancellationToken cancellationToken);
    Task<CallResult> TransferCallAsync(string providerCallId, string destination, CancellationToken cancellationToken);
    Task<CallResult> StartRecordingAsync(string providerCallId, CancellationToken cancellationToken);
    Task<CallResult> StopRecordingAsync(string providerCallId, CancellationToken cancellationToken);

    Task<bool> ValidateWebhookAsync(WebhookValidationContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Parses a validated webhook payload into a normalized event. Returns null if the payload doesn't
    /// map to anything actionable. Deliberately returns data instead of writing to the database itself —
    /// a provider adapter should only know how to speak its provider's format, not our schema or SignalR;
    /// <see cref="IVoiceService"/> is what applies the result.
    /// </summary>
    Task<VoiceWebhookEvent?> ProcessWebhookAsync(string payload, CancellationToken cancellationToken);
}

public interface IVoiceProviderFactory
{
    IVoiceProvider Resolve(string providerName);
}
