using Microsoft.AspNetCore.SignalR;
using Omni.Application.Interfaces;

namespace Omni.Api.Hubs;

public sealed class SignalRVoiceNotifier : IVoiceNotifier
{
    private readonly IHubContext<VoiceHub> _hubContext;

    public SignalRVoiceNotifier(IHubContext<VoiceHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyAsync(Guid organizationId, string eventName, object payload, CancellationToken cancellationToken) =>
        _hubContext.Clients.Group(VoiceHub.OrganizationGroup(organizationId)).SendAsync(eventName, payload, cancellationToken);
}
