using Microsoft.Extensions.Logging;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;

namespace Omni.Infrastructure.Providers;

public sealed class TemplateMessageService : ITemplateMessageService
{
    private readonly IMessageRepository _messageRepository;
    private readonly IMetaMessageSender _metaMessageSender;
    private readonly ILogger<TemplateMessageService> _logger;

    public TemplateMessageService(IMessageRepository messageRepository, IMetaMessageSender metaMessageSender, ILogger<TemplateMessageService> logger)
    {
        _messageRepository = messageRepository;
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
        CancellationToken cancellationToken)
    {
        var message = new Message
        {
            OrganizationId = organizationId,
            ConversationId = conversationId,
            Direction = "Outbound",
            MessageType = "Template",
            Body = RenderTemplateBody(template.BodyText, bodyParameters),
            Status = "Queued"
        };
        var messageId = await _messageRepository.CreateAsync(message, cancellationToken);

        try
        {
            var result = await _metaMessageSender.SendTemplateAsync(
                channelAccount.AccessToken!, channelAccount.ExternalAccountId!, recipientWhatsAppNumber,
                template.Name, template.Language, template.Category, bodyParameters, cancellationToken);
            message.ExternalMessageId = result.ExternalMessageId;
            message.Status = "Sent";
            await _messageRepository.UpdateAsync(message, cancellationToken);
            return new TemplateSendResult(true, messageId, result.ExternalMessageId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp template {TemplateName} for conversation {ConversationId}", template.Name, conversationId);
            message.Status = "Failed";
            await _messageRepository.UpdateAsync(message, cancellationToken);
            return new TemplateSendResult(false, messageId, null, $"Template send failed: {ex.Message}");
        }
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
