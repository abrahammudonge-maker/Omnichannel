namespace Omni.Application.Interfaces;

public interface IMetaMessageSender
{
    /// <param name="channelType">"WhatsApp", "FacebookMessenger", or "Instagram".</param>
    /// <param name="platformId">Phone Number ID (WhatsApp), Page ID (Messenger), or IG Business Account ID (Instagram).</param>
    /// <param name="recipientId">The customer's WhatsApp number, Messenger PSID, or Instagram-scoped ID.</param>
    Task SendAsync(
        string channelType,
        string accessToken,
        string platformId,
        string recipientId,
        string body,
        CancellationToken cancellationToken);
}
