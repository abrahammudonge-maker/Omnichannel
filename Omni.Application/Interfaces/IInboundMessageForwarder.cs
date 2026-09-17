using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

/// <summary>
/// Forwards a newly-received inbound message to an external system in real time, if that organization
/// has configured a forwarding webhook (via the "IntegrationWebhookUrl" / "IntegrationWebhookSecret"
/// organization settings). A no-op when unconfigured — this lets an external platform (e.g. a
/// server-to-server integration holding an API key for this organization) receive customer replies
/// without polling, mirroring how Meta's own webhooks push events to us.
/// </summary>
public interface IInboundMessageForwarder
{
    Task ForwardAsync(Guid organizationId, Conversation conversation, Customer customer, Message message, string publicBaseUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Forwards a delivery-status change (sent/delivered/read/failed) for a previously-sent outbound
    /// message, keyed by the external (Meta) message id the receiving system already has on file.
    /// </summary>
    Task ForwardStatusUpdateAsync(Guid organizationId, string externalMessageId, string status, CancellationToken cancellationToken);
}
