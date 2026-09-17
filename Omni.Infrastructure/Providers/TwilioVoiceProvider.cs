using Omni.Application.Interfaces;
using Omni.Domain.Constants;

namespace Omni.Infrastructure.Providers;

/// <summary>
/// Placeholder only — registered and selectable via <see cref="VoiceProviderFactory"/> so the rest of
/// the application (controllers, VoiceService, DI wiring) compiles and works against the real shape a
/// live Twilio integration will eventually have, without making any real API calls yet. Every method
/// fails clearly rather than pretending to succeed. Swap this out for a real implementation (Twilio
/// Voice REST API + TwiML + webhook signature validation via X-Twilio-Signature) in a later sprint,
/// once real provider credentials are ready to be wired up.
/// </summary>
public sealed class TwilioVoiceProvider : IVoiceProvider
{
    private const string NotImplementedMessage = "Twilio voice integration is not implemented yet — this is a placeholder for a future sprint.";

    public string Name => VoiceProviderName.Twilio;

    public Task<CallResult> InitiateCallAsync(InitiateCallRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Fail(NotImplementedMessage));

    public Task<CallResult> AnswerCallAsync(string providerCallId, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Fail(NotImplementedMessage));

    public Task<CallResult> HangupCallAsync(string providerCallId, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Fail(NotImplementedMessage));

    public Task<CallResult> HoldCallAsync(string providerCallId, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Fail(NotImplementedMessage));

    public Task<CallResult> ResumeCallAsync(string providerCallId, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Fail(NotImplementedMessage));

    public Task<CallResult> TransferCallAsync(string providerCallId, string destination, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Fail(NotImplementedMessage));

    public Task<CallResult> StartRecordingAsync(string providerCallId, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Fail(NotImplementedMessage));

    public Task<CallResult> StopRecordingAsync(string providerCallId, CancellationToken cancellationToken) =>
        Task.FromResult(CallResult.Fail(NotImplementedMessage));

    public Task<bool> ValidateWebhookAsync(WebhookValidationContext context, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<VoiceWebhookEvent?> ProcessWebhookAsync(string payload, CancellationToken cancellationToken) =>
        Task.FromResult<VoiceWebhookEvent?>(null);
}
