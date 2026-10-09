using Microsoft.Extensions.Logging;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;

namespace Omni.Infrastructure.Providers;

public sealed class TemplateMessageService : ITemplateMessageService
{
    private readonly IMessageRepository _messageRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IConversationStatusRepository _conversationStatusRepository;
    private readonly IMetaMessageSender _metaMessageSender;
    private readonly ILogger<TemplateMessageService> _logger;

    public TemplateMessageService(
        IMessageRepository messageRepository,
        IConversationRepository conversationRepository,
        IConversationStatusRepository conversationStatusRepository,
        IMetaMessageSender metaMessageSender,
        ILogger<TemplateMessageService> logger)
    {
        _messageRepository = messageRepository;
        _conversationRepository = conversationRepository;
        _conversationStatusRepository = conversationStatusRepository;
        _metaMessageSender = metaMessageSender;
        _logger = logger;
    }

    public async Task<TemplateSendResult> SendAsync(
        Guid organizationId,
        Guid conversationId,
        ChannelAccount channelAccount,
        MessageTemplate template,
        string recipientWhatsAppNumber,
        IReadOnlyList<string> bodyParameters,
        CancellationToken cancellationToken,
        Guid? retryOfMessageId = null)
    {
        // A retry reuses the message row of the failed attempt, so the inbox shows one message, not one per attempt.
        var message = retryOfMessageId is null ? null : await _messageRepository.GetByIdAsync(retryOfMessageId.Value, organizationId, cancellationToken);
        Guid messageId;
        if (message is not null && message.ConversationId == conversationId)
        {
            message.Status = "Queued";
            await _messageRepository.UpdateAsync(message, cancellationToken);
            messageId = message.Id;
        }
        else
        {
            message = new Message
            {
                OrganizationId = organizationId,
                ConversationId = conversationId,
                Direction = "Outbound",
                MessageType = "Template",
                Body = RenderTemplateBody(template.BodyText, bodyParameters),
                Status = "Queued"
            };
            messageId = await _messageRepository.CreateAsync(message, cancellationToken);
        }

        try
        {
            var result = await _metaMessageSender.SendTemplateAsync(
                channelAccount.AccessToken!, channelAccount.ExternalAccountId!, recipientWhatsAppNumber,
                template.Name, template.Language, template.Category, bodyParameters, cancellationToken);
            message.ExternalMessageId = result.ExternalMessageId;
            message.Status = "Sent";
            await _messageRepository.UpdateAsync(message, cancellationToken);
            await ReopenIfClosedAsync(organizationId, conversationId, cancellationToken);
            return new TemplateSendResult(true, messageId, result.ExternalMessageId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp template {TemplateName} for conversation {ConversationId}", template.Name, conversationId);
            message.Status = "Failed";
            await _messageRepository.UpdateAsync(message, cancellationToken);
            var reason = ex is MetaApiException { Details: not null } meta
                ? meta.ErrorCode is null ? meta.Details : $"{meta.Details} (Meta error {meta.ErrorCode})"
                : ex.Message;
            return new TemplateSendResult(false, messageId, null, $"Template send failed: {reason}", MetaApiException.IsTransientFailure(ex));
        }
    }

    /// <summary>
    /// The Meta webhook only files inbound messages under an open conversation, so a template sent from a
    /// Closed/Resolved conversation would have its reply land in a new one. Reopen it so the reply threads here.
    /// </summary>
    private async Task ReopenIfClosedAsync(Guid organizationId, Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId, organizationId, cancellationToken);
        if (conversation is null || conversation.Status is not ("Closed" or "Resolved"))
        {
            return;
        }

        await _conversationStatusRepository.CreateAsync(new ConversationStatusHistory
        {
            OrganizationId = organizationId,
            ConversationId = conversationId,
            Status = "Open",
            ChangedBy = null,
            Reason = "Reopened automatically after sending a WhatsApp template."
        }, cancellationToken);

        conversation.Status = "Open";
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);
    }

    private static string RenderTemplateBody(string bodyText, IReadOnlyList<string> parameters)
    {
        var rendered = bodyText;
        for (var i = 0; i < parameters.Count; i++)
        {
            rendered = rendered.Replace($"{{{{{i + 1}}}}}", parameters[i]);
        }
        return rendered;
    }
}
