using Microsoft.Extensions.Logging;
using Omni.Application.Interfaces;
using Omni.Domain.Constants;
using Omni.Domain.Entities;
using Omni.Domain.Enums;

namespace Omni.Application.Services;

public sealed class VoiceService : IVoiceService
{
    private readonly ICallRepository _callRepository;
    private readonly ICallEventRepository _callEventRepository;
    private readonly IPhoneNumberRepository _phoneNumberRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly ICallQueueRepository _callQueueRepository;
    private readonly ICallQueueMemberRepository _callQueueMemberRepository;
    private readonly IVoiceProviderFactory _voiceProviderFactory;
    private readonly IVoiceNotifier _voiceNotifier;
    private readonly ILogger<VoiceService> _logger;

    public VoiceService(
        ICallRepository callRepository,
        ICallEventRepository callEventRepository,
        IPhoneNumberRepository phoneNumberRepository,
        ICustomerRepository customerRepository,
        IConversationRepository conversationRepository,
        ICallQueueRepository callQueueRepository,
        ICallQueueMemberRepository callQueueMemberRepository,
        IVoiceProviderFactory voiceProviderFactory,
        IVoiceNotifier voiceNotifier,
        ILogger<VoiceService> logger)
    {
        _callRepository = callRepository;
        _callEventRepository = callEventRepository;
        _phoneNumberRepository = phoneNumberRepository;
        _customerRepository = customerRepository;
        _conversationRepository = conversationRepository;
        _callQueueRepository = callQueueRepository;
        _callQueueMemberRepository = callQueueMemberRepository;
        _voiceProviderFactory = voiceProviderFactory;
        _voiceNotifier = voiceNotifier;
        _logger = logger;
    }

    public async Task<CallActionResult> InitiateCallAsync(Guid organizationId, Guid agentId, Guid customerId, Guid phoneNumberId, Guid? conversationId, CancellationToken cancellationToken)
    {
        var phoneNumber = await _phoneNumberRepository.GetByIdAsync(phoneNumberId, organizationId, cancellationToken);
        if (phoneNumber is null)
        {
            return CallActionResult.AsNotFound("Phone number not found.");
        }
        if (!string.Equals(phoneNumber.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return CallActionResult.Fail("This phone number is not active.");
        }

        var customer = await _customerRepository.GetByIdAsync(customerId, organizationId, cancellationToken);
        if (customer is null)
        {
            return CallActionResult.AsNotFound("Customer not found.");
        }
        if (string.IsNullOrWhiteSpace(customer.Phone))
        {
            return CallActionResult.Fail("This customer has no phone number on file.");
        }

        if (conversationId is Guid convId)
        {
            var conversation = await _conversationRepository.GetByIdAsync(convId, organizationId, cancellationToken);
            if (conversation is null || conversation.CustomerId != customerId)
            {
                return CallActionResult.Fail("The specified conversation doesn't belong to this customer.");
            }
        }

        var call = new Call
        {
            OrganizationId = organizationId,
            CustomerId = customerId,
            ConversationId = conversationId,
            AgentId = agentId,
            Provider = phoneNumber.Provider,
            Direction = CallDirection.Outbound,
            FromNumber = phoneNumber.Number,
            ToNumber = customer.Phone,
            Status = CallStatus.Ringing,
            StartedAt = DateTimeOffset.UtcNow
        };
        // Persisted before contacting the provider (per spec) so every outbound attempt has an internal id
        // even if the provider call below fails.
        var callId = await _callRepository.CreateAsync(call, cancellationToken);

        var provider = _voiceProviderFactory.Resolve(phoneNumber.Provider);
        var request = new InitiateCallRequest(organizationId, phoneNumber.Number, customer.Phone, phoneNumber.ProviderNumberId, null);

        CallResult result;
        try
        {
            result = await provider.InitiateCallAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Voice provider {Provider} failed to initiate call {CallId}", phoneNumber.Provider, callId);
            result = CallResult.Fail($"Provider call failed: {ex.Message}");
        }

        var stored = await _callRepository.GetByIdAsync(callId, organizationId, cancellationToken) ?? call;
        if (!result.Success)
        {
            stored.Status = CallStatus.Failed;
            await _callRepository.UpdateAsync(stored, cancellationToken);
            await NotifyAsync(organizationId, "CallFailed", stored, cancellationToken);
            return CallActionResult.Fail(result.ErrorMessage ?? "Call failed.", stored);
        }

        stored.ProviderCallId = result.ProviderCallId;
        if (!string.IsNullOrWhiteSpace(result.Status))
        {
            stored.Status = result.Status!;
        }
        await _callRepository.UpdateAsync(stored, cancellationToken);
        await NotifyAsync(organizationId, "CallRinging", stored, cancellationToken);
        return CallActionResult.Ok(stored);
    }

    public Task<CallActionResult> HangupAsync(Guid organizationId, Guid callId, CancellationToken cancellationToken) =>
        ExecuteActionAsync(organizationId, callId, (provider, providerCallId, ct) => provider.HangupCallAsync(providerCallId, ct), ApplyHangupOutcome, "CallEnded", cancellationToken);

    public Task<CallActionResult> HoldAsync(Guid organizationId, Guid callId, CancellationToken cancellationToken) =>
        ExecuteActionAsync(organizationId, callId, (provider, providerCallId, ct) => provider.HoldCallAsync(providerCallId, ct), call => call.Status = CallStatus.OnHold, "CallHeld", cancellationToken);

    public Task<CallActionResult> ResumeAsync(Guid organizationId, Guid callId, CancellationToken cancellationToken) =>
        ExecuteActionAsync(organizationId, callId, (provider, providerCallId, ct) => provider.ResumeCallAsync(providerCallId, ct), call => call.Status = CallStatus.Answered, "CallResumed", cancellationToken);

    public Task<CallActionResult> TransferAsync(Guid organizationId, Guid callId, string destination, CancellationToken cancellationToken) =>
        ExecuteActionAsync(organizationId, callId, (provider, providerCallId, ct) => provider.TransferCallAsync(providerCallId, destination, ct), call => call.Status = CallStatus.Transferred, "CallTransferred", cancellationToken);

    public Task<CallActionResult> StartRecordingAsync(Guid organizationId, Guid callId, CancellationToken cancellationToken) =>
        ExecuteActionAsync(organizationId, callId, (provider, providerCallId, ct) => provider.StartRecordingAsync(providerCallId, ct), call => call.RecordingStatus = RecordingStatus.Processing, null, cancellationToken);

    public Task<CallActionResult> StopRecordingAsync(Guid organizationId, Guid callId, CancellationToken cancellationToken) =>
        ExecuteActionAsync(organizationId, callId, (provider, providerCallId, ct) => provider.StopRecordingAsync(providerCallId, ct), call => call.RecordingStatus = RecordingStatus.Processing, null, cancellationToken);

    private static void ApplyHangupOutcome(Call call)
    {
        call.Status = CallStatus.Completed;
        call.EndedAt = DateTimeOffset.UtcNow;
        var referenceStart = call.AnsweredAt ?? call.StartedAt;
        if (referenceStart is DateTimeOffset start)
        {
            call.DurationSeconds = Math.Max(0, (int)(call.EndedAt.Value - start).TotalSeconds);
        }
    }

    private async Task<CallActionResult> ExecuteActionAsync(
        Guid organizationId,
        Guid callId,
        Func<IVoiceProvider, string, CancellationToken, Task<CallResult>> providerAction,
        Action<Call> applyOnSuccess,
        string? notifyEventName,
        CancellationToken cancellationToken)
    {
        var call = await _callRepository.GetByIdAsync(callId, organizationId, cancellationToken);
        if (call is null)
        {
            return CallActionResult.AsNotFound("Call not found.");
        }
        if (string.IsNullOrWhiteSpace(call.ProviderCallId))
        {
            return CallActionResult.Fail("This call hasn't been connected to a provider yet.", call);
        }

        var provider = _voiceProviderFactory.Resolve(call.Provider);
        CallResult result;
        try
        {
            result = await providerAction(provider, call.ProviderCallId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Voice provider {Provider} action failed for call {CallId}", call.Provider, callId);
            return CallActionResult.Fail($"Provider action failed: {ex.Message}", call);
        }

        if (!result.Success)
        {
            return CallActionResult.Fail(result.ErrorMessage ?? "Provider rejected the request.", call);
        }

        applyOnSuccess(call);
        await _callRepository.UpdateAsync(call, cancellationToken);
        if (notifyEventName is not null)
        {
            await NotifyAsync(organizationId, notifyEventName, call, cancellationToken);
        }
        return CallActionResult.Ok(call);
    }

    public async Task ProcessWebhookAsync(string providerName, VoiceWebhookEvent webhookEvent, string rawPayload, CancellationToken cancellationToken)
    {
        var call = await _callRepository.FindByProviderCallIdAsync(providerName, webhookEvent.ProviderCallId, cancellationToken);
        var organizationId = call?.OrganizationId;

        if (call is null)
        {
            call = await TryCreateInboundCallAsync(providerName, webhookEvent, cancellationToken);
            if (call is null)
            {
                _logger.LogWarning("Voice webhook for {Provider} call {ProviderCallId} matched no known call and no known phone number — dropped.", providerName, webhookEvent.ProviderCallId);
                return;
            }
            organizationId = call.OrganizationId;
        }

        ApplyWebhookEvent(call, webhookEvent);
        await _callRepository.UpdateAsync(call, cancellationToken);

        await _callEventRepository.CreateAsync(new CallEvent
        {
            OrganizationId = organizationId!.Value,
            CallId = call.Id,
            EventType = webhookEvent.EventType,
            ProviderEventId = webhookEvent.ProviderEventId,
            Payload = rawPayload
        }, cancellationToken);

        var signalrEvent = MapToSignalREventName(webhookEvent, call);
        if (signalrEvent is not null)
        {
            await NotifyAsync(organizationId.Value, signalrEvent, call, cancellationToken);
        }
    }

    private async Task<Call?> TryCreateInboundCallAsync(string providerName, VoiceWebhookEvent webhookEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webhookEvent.ToNumber) || string.IsNullOrWhiteSpace(webhookEvent.FromNumber))
        {
            return null;
        }

        var phoneNumber = await _phoneNumberRepository.FindByNumberAsync(webhookEvent.ToNumber, cancellationToken);
        if (phoneNumber is null)
        {
            return null;
        }

        var customer = await FindOrCreateCustomerAsync(phoneNumber.OrganizationId, webhookEvent.FromNumber, cancellationToken);
        var conversation = await FindOrCreateConversationAsync(phoneNumber.OrganizationId, customer.Id, cancellationToken);

        var call = new Call
        {
            OrganizationId = phoneNumber.OrganizationId,
            CustomerId = customer.Id,
            ConversationId = conversation.Id,
            Provider = providerName,
            ProviderCallId = webhookEvent.ProviderCallId,
            Direction = CallDirection.Inbound,
            FromNumber = webhookEvent.FromNumber,
            ToNumber = webhookEvent.ToNumber,
            Status = CallStatus.Ringing,
            StartedAt = webhookEvent.Timestamp ?? DateTimeOffset.UtcNow
        };
        var callId = await _callRepository.CreateAsync(call, cancellationToken);
        return await _callRepository.GetByIdAsync(callId, phoneNumber.OrganizationId, cancellationToken);
    }

    private async Task<Customer> FindOrCreateCustomerAsync(Guid organizationId, string phoneNumber, CancellationToken cancellationToken)
    {
        var customers = await _customerRepository.GetAllAsync(organizationId, cancellationToken);
        var existing = customers.FirstOrDefault(c => string.Equals(c.Phone, phoneNumber, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var customer = new Customer
        {
            OrganizationId = organizationId,
            FullName = phoneNumber,
            Phone = phoneNumber,
            Email = string.Empty
        };
        await _customerRepository.CreateAsync(customer, cancellationToken);
        return customer;
    }

    private async Task<Conversation> FindOrCreateConversationAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken)
    {
        var conversations = await _conversationRepository.GetAllAsync(organizationId, cancellationToken);
        var existing = conversations.FirstOrDefault(c => c.CustomerId == customerId && c.Channel == Channel.Voice);
        if (existing is not null)
        {
            return existing;
        }

        var conversation = new Conversation
        {
            OrganizationId = organizationId,
            CustomerId = customerId,
            Channel = Channel.Voice,
            Status = "Open"
        };
        await _conversationRepository.CreateAsync(conversation, cancellationToken);
        return conversation;
    }

    private static void ApplyWebhookEvent(Call call, VoiceWebhookEvent webhookEvent)
    {
        if (!string.IsNullOrWhiteSpace(webhookEvent.Status))
        {
            call.Status = webhookEvent.Status!;
            if (string.Equals(webhookEvent.Status, CallStatus.Answered, StringComparison.OrdinalIgnoreCase) && call.AnsweredAt is null)
            {
                call.AnsweredAt = webhookEvent.Timestamp ?? DateTimeOffset.UtcNow;
            }
            if (CallStatus.Terminal.Contains(webhookEvent.Status!) && call.EndedAt is null)
            {
                call.EndedAt = webhookEvent.Timestamp ?? DateTimeOffset.UtcNow;
            }
        }

        if (webhookEvent.DurationSeconds is int duration)
        {
            call.DurationSeconds = duration;
        }
        if (!string.IsNullOrWhiteSpace(webhookEvent.RecordingUrl))
        {
            call.RecordingUrl = webhookEvent.RecordingUrl;
        }
        if (!string.IsNullOrWhiteSpace(webhookEvent.RecordingStatus))
        {
            call.RecordingStatus = webhookEvent.RecordingStatus!;
        }
    }

    private static string? MapToSignalREventName(VoiceWebhookEvent webhookEvent, Call call)
    {
        var status = webhookEvent.Status;
        if (status is null) return null;

        if (Is(status, CallStatus.Ringing)) return call.Direction == CallDirection.Inbound ? "CallIncoming" : "CallRinging";
        if (Is(status, CallStatus.Answered)) return "CallAnswered";
        if (Is(status, CallStatus.OnHold)) return "CallHeld";
        if (Is(status, CallStatus.Transferred)) return "CallTransferred";
        if (Is(status, CallStatus.Completed)) return !string.IsNullOrWhiteSpace(call.RecordingUrl) ? "RecordingAvailable" : "CallEnded";
        if (Is(status, CallStatus.Missed)) return "CallMissed";
        if (Is(status, CallStatus.Failed) || Is(status, CallStatus.Rejected)) return "CallFailed";
        return null;

        static bool Is(string value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Guid?> SelectAgentForQueueAsync(Guid queueId, Guid organizationId, CancellationToken cancellationToken)
    {
        var queue = await _callQueueRepository.GetByIdAsync(queueId, organizationId, cancellationToken);
        if (queue is null || !queue.IsActive || string.Equals(queue.Strategy, CallQueueStrategy.Manual, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var members = (await _callQueueMemberRepository.GetByQueueIdAsync(queueId, organizationId, cancellationToken))
            .Where(m => m.IsActive)
            .OrderBy(m => m.Priority)
            .ToList();
        if (members.Count == 0)
        {
            return null;
        }

        if (string.Equals(queue.Strategy, CallQueueStrategy.LeastBusy, StringComparison.OrdinalIgnoreCase))
        {
            var counts = await Task.WhenAll(members.Select(async m => (m.UserId, Count: await _callRepository.CountActiveCallsForAgentAsync(m.UserId, organizationId, cancellationToken))));
            return counts.OrderBy(c => c.Count).First().UserId;
        }

        // RoundRobin and LongestIdle both resolve to "whoever went longest without a new assignment" —
        // a reasonable stand-in for either given this app has no live agent-presence tracking yet.
        var lastAssigned = await Task.WhenAll(members.Select(async m => (m.UserId, LastAssignedAt: await _callRepository.GetLastAssignedAtForAgentAsync(m.UserId, organizationId, cancellationToken))));
        return lastAssigned.OrderBy(a => a.LastAssignedAt ?? DateTimeOffset.MinValue).First().UserId;
    }

    private Task NotifyAsync(Guid organizationId, string eventName, Call call, CancellationToken cancellationToken) =>
        _voiceNotifier.NotifyAsync(organizationId, eventName, new
        {
            call.Id,
            call.CustomerId,
            call.ConversationId,
            call.AgentId,
            call.Direction,
            call.FromNumber,
            call.ToNumber,
            call.Status,
            call.RecordingStatus,
            call.RecordingUrl,
            call.DurationSeconds
        }, cancellationToken);
}
