using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

/// <summary>Pulls a WhatsApp number's template catalog (names, statuses, bodies) from Meta into message_templates.</summary>
public interface IWhatsAppTemplateSyncService
{
    /// <summary>True when the account has what a sync needs: a WABA id and an access token.</summary>
    bool CanSync(ChannelAccount account);

    /// <summary>Syncs now and returns how many templates Meta returned. Throws if Meta rejects the call.</summary>
    Task<int> SyncAsync(ChannelAccount account, CancellationToken cancellationToken);

    /// <summary>
    /// Syncs unless this account was synced in the last minute. Never throws. Used when a caller asks for a
    /// template we don't know yet, so a template approved minutes ago works without an admin pressing Sync.
    /// </summary>
    Task<bool> TrySyncIfStaleAsync(ChannelAccount account, CancellationToken cancellationToken);
}
