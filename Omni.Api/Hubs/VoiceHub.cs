using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Omni.Api.Hubs;

/// <summary>
/// Real-time channel for call events (CallIncoming, CallRinging, CallAnswered, CallHeld, CallResumed,
/// CallTransferred, CallEnded, CallMissed, CallFailed, RecordingAvailable). Clients don't call anything
/// on this hub — they just connect and listen; every connection is placed into a group scoped to its
/// own organization so one tenant never receives another tenant's call events.
/// </summary>
[Authorize]
public sealed class VoiceHub : Hub
{
    public static string OrganizationGroup(Guid organizationId) => $"org:{organizationId}";

    public override async Task OnConnectedAsync()
    {
        var organizationIdClaim = Context.User?.Claims.FirstOrDefault(c => c.Type == "OrganizationId")?.Value;
        if (Guid.TryParse(organizationIdClaim, out var organizationId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, OrganizationGroup(organizationId));
        }

        await base.OnConnectedAsync();
    }
}
