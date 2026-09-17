using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public sealed record CallActionResult(bool Success, Call? Call, string? ErrorMessage, bool NotFound = false)
{
    public static CallActionResult Ok(Call call) => new(true, call, null);
    public static CallActionResult Fail(string error, Call? call = null) => new(false, call, error);
    public static CallActionResult AsNotFound(string error) => new(false, null, error, true);
}

/// <summary>Orchestrates a call across the repositories, the resolved IVoiceProvider, and real-time notifications. Provider-agnostic — every method resolves the right IVoiceProvider via IVoiceProviderFactory internally.</summary>
public interface IVoiceService
{
    Task<CallActionResult> InitiateCallAsync(Guid organizationId, Guid agentId, Guid customerId, Guid phoneNumberId, Guid? conversationId, CancellationToken cancellationToken);
    Task<CallActionResult> HangupAsync(Guid organizationId, Guid callId, CancellationToken cancellationToken);
    Task<CallActionResult> HoldAsync(Guid organizationId, Guid callId, CancellationToken cancellationToken);
    Task<CallActionResult> ResumeAsync(Guid organizationId, Guid callId, CancellationToken cancellationToken);
    Task<CallActionResult> TransferAsync(Guid organizationId, Guid callId, string destination, CancellationToken cancellationToken);
    Task<CallActionResult> StartRecordingAsync(Guid organizationId, Guid callId, CancellationToken cancellationToken);
    Task<CallActionResult> StopRecordingAsync(Guid organizationId, Guid callId, CancellationToken cancellationToken);

    /// <summary>Applies an already-parsed, already-validated webhook event: updates the Call row, records a CallEvent, and raises a real-time notification. The organization is derived from the matched Call/PhoneNumber, never trusted from the payload.</summary>
    Task ProcessWebhookAsync(string providerName, VoiceWebhookEvent webhookEvent, string rawPayload, CancellationToken cancellationToken);

    /// <summary>Picks the next agent for a queue per its configured strategy. Returns null for an empty/inactive queue, or for Manual strategy (which never auto-assigns).</summary>
    Task<Guid?> SelectAgentForQueueAsync(Guid queueId, Guid organizationId, CancellationToken cancellationToken);
}

/// <summary>Publishes real-time call events. Kept framework-agnostic here — the SignalR-backed implementation lives in the API layer, where the Hub type is defined.</summary>
public interface IVoiceNotifier
{
    Task NotifyAsync(Guid organizationId, string eventName, object payload, CancellationToken cancellationToken);
}
