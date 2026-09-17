namespace Omni.Application.Interfaces;

public sealed record MetaMessageSendResult(string ExternalMessageId);
public sealed record MetaOutboundAttachment(string ContentType, string FileName, Stream Content);

public interface IMetaMessageSender
{
    /// <param name="channelType">"WhatsApp", "FacebookMessenger", or "Instagram".</param>
    /// <param name="platformId">Phone Number ID (WhatsApp), Page ID (Messenger), or IG Business Account ID (Instagram).</param>
    /// <param name="recipientId">The customer's WhatsApp number, Messenger PSID, or Instagram-scoped ID.</param>
    /// <param name="attachment">Optional file to send as a media message. When present alongside a non-empty body on Messenger/Instagram, the attachment and text are sent as two separate messages (the platform doesn't support both in one).</param>
    Task<MetaMessageSendResult> SendAsync(
        string channelType,
        string accessToken,
        string platformId,
        string recipientId,
        string body,
        MetaOutboundAttachment? attachment,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sends a pre-approved WhatsApp message template (required to reach a customer outside the
    /// 24-hour customer service window). <paramref name="bodyParameters"/> are substituted in order
    /// for {{1}}, {{2}}, ... in the template's BODY component; pass an empty list for a template with no variables.
    /// When <paramref name="category"/> is "AUTHENTICATION", <paramref name="bodyParameters"/> must contain exactly
    /// one value (the code) — it's also echoed onto the template's mandatory OTP button automatically
    /// (Meta requires AUTHENTICATION templates to have exactly one such button; there's no way to omit it).
    /// </summary>
    Task<MetaMessageSendResult> SendTemplateAsync(
        string accessToken,
        string phoneNumberId,
        string recipientId,
        string templateName,
        string language,
        string category,
        IReadOnlyList<string> bodyParameters,
        CancellationToken cancellationToken);
}
