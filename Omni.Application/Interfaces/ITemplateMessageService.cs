using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public sealed record TemplateSendResult(bool Success, Guid MessageId, string? ExternalMessageId, string? ErrorMessage);

/// <summary>
/// Renders and sends an approved WhatsApp template message, and records the resulting Message row.
/// Shared by the agent-facing send-template endpoint and the API-key integration endpoint, so both
/// paths behave identically — only how the conversation/recipient gets resolved differs between them.
/// </summary>
public interface ITemplateMessageService
{
    Task<TemplateSendResult> SendAsync(
        Guid organizationId,
        Guid conversationId,
        ChannelAccount channelAccount,
        MessageTemplate template,
        string recipientWhatsAppNumber,
        IReadOnlyList<string> bodyParameters,
        CancellationToken cancellationToken);
}
