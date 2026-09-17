using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Domain.Enums;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireAgent")]
[Route("api/[controller]")]
public sealed class MessagesController : ControllerBase
{
    private readonly IMessageRepository _messageRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IChannelAccountRepository _channelAccountRepository;
    private readonly IMessageTemplateRepository _messageTemplateRepository;
    private readonly ITemplateMessageService _templateMessageService;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IEmailSender _emailSender;
    private readonly IMetaMessageSender _metaMessageSender;
    private readonly ISmsSender _smsSender;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(
        IMessageRepository messageRepository,
        IConversationRepository conversationRepository,
        ICustomerRepository customerRepository,
        IChannelAccountRepository channelAccountRepository,
        IMessageTemplateRepository messageTemplateRepository,
        ITemplateMessageService templateMessageService,
        IAttachmentRepository attachmentRepository,
        IEmailSender emailSender,
        IMetaMessageSender metaMessageSender,
        ISmsSender smsSender,
        ILogger<MessagesController> logger)
    {
        _messageRepository = messageRepository;
        _conversationRepository = conversationRepository;
        _customerRepository = customerRepository;
        _channelAccountRepository = channelAccountRepository;
        _messageTemplateRepository = messageTemplateRepository;
        _templateMessageService = templateMessageService;
        _attachmentRepository = attachmentRepository;
        _emailSender = emailSender;
        _metaMessageSender = metaMessageSender;
        _smsSender = smsSender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Message>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _messageRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Message>>.Ok(result, "Messages retrieved successfully."));
    }

    [HttpGet("conversation/{conversationId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Message>>>> GetByConversationId(Guid conversationId, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _messageRepository.GetByConversationIdAsync(conversationId, organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Message>>.Ok(result, "Messages retrieved successfully."));
    }

    [HttpPost]
    [EnableRateLimiting("messages")]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateMessageRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, organizationId, cancellationToken);
        if (conversation is null) return NotFound(ApiResponse<Guid>.Fail("Conversation not found."));

        Attachment? attachment = null;
        if (request.AttachmentId is Guid attachmentId)
        {
            attachment = await _attachmentRepository.GetByIdAsync(attachmentId, organizationId, cancellationToken);
            if (attachment is null || attachment.ConversationId != request.ConversationId)
                return BadRequest(ApiResponse<Guid>.Fail("Attachment not found on this conversation."));
        }

        if (string.IsNullOrWhiteSpace(request.Body) && attachment is null)
            return BadRequest(ApiResponse<Guid>.Fail("Message body is required."));

        var message = new Message
        {
            OrganizationId = organizationId,
            ConversationId = request.ConversationId,
            Direction = "Outbound",
            MessageType = attachment is not null ? Attachment.MessageTypeForContentType(attachment.ContentType) : request.MessageType,
            Body = request.Body,
            AttachmentUrl = attachment is not null ? $"/api/attachments/{attachment.Id}/download" : request.AttachmentUrl,
            Status = "Queued"
        };

        var id = await _messageRepository.CreateAsync(message, cancellationToken);

        {
            var sendResult = conversation?.Channel switch
            {
                Channel.Email => await TrySendEmailAsync(organizationId, conversation, request.Body, attachment, cancellationToken),
                Channel.WhatsApp or Channel.FacebookMessenger or Channel.Instagram =>
                    await TrySendMetaMessageAsync(organizationId, conversation!, message, request.Body, attachment, cancellationToken),
                Channel.Sms => await TrySendSmsAsync(organizationId, conversation!, message, request.Body, cancellationToken),
                _ => null
            };

            if (sendResult is not null)
            {
                message.Status = sendResult.Value.Success ? "Sent" : "Failed";
                await _messageRepository.UpdateAsync(message, cancellationToken);
                if (!sendResult.Value.Success)
                {
                    return Ok(ApiResponse<Guid>.Fail($"Message saved, but not delivered. {sendResult.Value.FailureReason}"));
                }
            }
        }

        return Ok(ApiResponse<Guid>.Ok(id, "Message created successfully."));
    }

    /// <summary>
    /// Sends a pre-approved WhatsApp message template — the only way to reach a customer outside the
    /// 24-hour customer service window (see the window check in TrySendMetaMessageAsync).
    /// </summary>
    [HttpPost("send-template")]
    [EnableRateLimiting("messages")]
    public async Task<ActionResult<ApiResponse<Guid>>> SendTemplate([FromBody] SendTemplateMessageRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, organizationId, cancellationToken);
        if (conversation is null) return NotFound(ApiResponse<Guid>.Fail("Conversation not found."));
        if (conversation.Channel != Channel.WhatsApp)
            return BadRequest(ApiResponse<Guid>.Fail("Message templates are only supported for WhatsApp conversations."));

        var template = await _messageTemplateRepository.GetByIdAsync(request.TemplateId, organizationId, cancellationToken);
        if (template is null) return NotFound(ApiResponse<Guid>.Fail("Template not found."));
        if (!string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<Guid>.Fail($"This template isn't approved yet (status: {template.Status})."));

        var customer = await _customerRepository.GetByIdAsync(conversation.CustomerId, organizationId, cancellationToken);
        if (customer is null || string.IsNullOrWhiteSpace(customer.WhatsAppNumber))
            return BadRequest(ApiResponse<Guid>.Fail("This customer has no WhatsApp contact on file."));

        var account = await _channelAccountRepository.GetByIdAsync(template.ChannelAccountId, organizationId, cancellationToken);
        if (account is null || account.Status != "Active" || string.IsNullOrWhiteSpace(account.AccessToken) || string.IsNullOrWhiteSpace(account.ExternalAccountId))
            return BadRequest(ApiResponse<Guid>.Fail("The WhatsApp channel this template belongs to is no longer connected."));

        var bodyParameters = request.BodyParameters ?? new List<string>();
        var result = await _templateMessageService.SendAsync(organizationId, conversation.Id, account, template, customer.WhatsAppNumber!, bodyParameters, cancellationToken);

        return result.Success
            ? Ok(ApiResponse<Guid>.Ok(result.MessageId, "Template message sent successfully."))
            : Ok(ApiResponse<Guid>.Fail($"Message saved, but not delivered. {result.ErrorMessage}"));
    }

    private async Task<(bool Success, string? FailureReason)?> TrySendEmailAsync(Guid organizationId, Conversation conversation, string body, Attachment? attachment, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(conversation.CustomerId, organizationId, cancellationToken);
        if (customer is null || string.IsNullOrWhiteSpace(customer.Email))
        {
            return (false, "This customer has no email address on file.");
        }

        var channelAccounts = await _channelAccountRepository.GetAllAsync(organizationId, cancellationToken);
        var emailAccount = channelAccounts.FirstOrDefault(a => a.ChannelType == "Email" && a.Status == "Active"
            && !string.IsNullOrWhiteSpace(a.ExternalAccountId) && !string.IsNullOrWhiteSpace(a.AccessToken));
        if (emailAccount is null)
        {
            return (false, "No active email mailbox is connected. Connect one under Settings → Channels.");
        }

        var preset = Omni.Infrastructure.Providers.EmailProviderPresets.Resolve(emailAccount.ExternalAccountId!);
        var smtpHost = emailAccount.SmtpHost ?? preset?.SmtpHost;
        var smtpPort = emailAccount.SmtpPort ?? preset?.SmtpPort;
        if (string.IsNullOrWhiteSpace(smtpHost) || smtpPort is null)
        {
            return (false, $"No SMTP server is configured for {emailAccount.ExternalAccountId} and it isn't a recognized provider. Set the SMTP host/port under Settings → Channels.");
        }

        try
        {
            await using var attachmentStream = attachment is not null ? System.IO.File.OpenRead(attachment.StoragePath) : null;
            var emailAttachment = attachment is not null && attachmentStream is not null
                ? new EmailAttachment(attachment.FileName, attachment.ContentType, attachmentStream)
                : null;

            await _emailSender.SendAsync(
                smtpHost,
                smtpPort.Value,
                emailAccount.ExternalAccountId!,
                emailAccount.AccessToken!,
                emailAccount.DisplayName,
                customer.Email,
                "Re: your conversation with us",
                body,
                emailAttachment,
                cancellationToken);
            return (true, null);
        }
        catch (MailKit.Security.AuthenticationException ex)
        {
            _logger.LogError(ex, "Email authentication failed for conversation {ConversationId}", conversation.Id);
            return (false, $"Could not authenticate with mailbox {emailAccount.ExternalAccountId} at {smtpHost}. Check the address and app password under Settings → Channels.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send outbound email for conversation {ConversationId}", conversation.Id);
            return (false, $"Email send failed: {ex.Message}");
        }
    }

    private async Task<(bool Success, string? FailureReason)?> TrySendMetaMessageAsync(Guid organizationId, Conversation conversation, Message message, string body, Attachment? attachment, CancellationToken cancellationToken)
    {
        var channelType = conversation.Channel.ToString();
        var customer = await _customerRepository.GetByIdAsync(conversation.CustomerId, organizationId, cancellationToken);

        var recipientId = conversation.Channel switch
        {
            Channel.WhatsApp => customer?.WhatsAppNumber,
            Channel.FacebookMessenger => customer?.FacebookId,
            Channel.Instagram => customer?.InstagramId,
            _ => null
        };

        if (customer is null || string.IsNullOrWhiteSpace(recipientId))
        {
            return (false, $"This customer has no {channelType} contact on file.");
        }

        var channelAccounts = await _channelAccountRepository.GetAllAsync(organizationId, cancellationToken);
        var account = conversation.ChannelAccountId is Guid channelAccountId
            ? channelAccounts.FirstOrDefault(a => a.Id == channelAccountId && a.ChannelType == channelType && a.Status == "Active"
                && !string.IsNullOrWhiteSpace(a.ExternalAccountId) && !string.IsNullOrWhiteSpace(a.AccessToken))
            : channelAccounts.FirstOrDefault(a => a.ChannelType == channelType && a.Status == "Active"
                && !string.IsNullOrWhiteSpace(a.ExternalAccountId) && !string.IsNullOrWhiteSpace(a.AccessToken));
        if (account is null)
        {
            return (false, $"No active {channelType} channel is connected. Connect one under Settings → Channels.");
        }

        var lastInboundSentAt = await _messageRepository.GetLastInboundSentAtAsync(conversation.Id, organizationId, cancellationToken);
        if (lastInboundSentAt is null || DateTimeOffset.UtcNow - lastInboundSentAt.Value > TimeSpan.FromHours(24))
        {
            var windowMessage = conversation.Channel == Channel.WhatsApp
                ? "This customer hasn't messaged in the last 24 hours (or has never messaged first). WhatsApp only allows free-text replies within 24 hours of the customer's last message — reaching them now requires a pre-approved message template, which isn't set up yet."
                : "This customer hasn't messaged in the last 24 hours (or has never messaged first). Meta only allows free-text replies within 24 hours of the customer's last message — reaching them now requires a message tag for a specific use case, which isn't set up yet.";
            return (false, windowMessage);
        }

        try
        {
            await using var attachmentStream = attachment is not null ? System.IO.File.OpenRead(attachment.StoragePath) : null;
            var metaAttachment = attachment is not null && attachmentStream is not null
                ? new MetaOutboundAttachment(attachment.ContentType, attachment.FileName, attachmentStream)
                : null;

            var result = await _metaMessageSender.SendAsync(channelType, account.AccessToken!, account.ExternalAccountId!, recipientId, body, metaAttachment, cancellationToken);
            message.ExternalMessageId = result.ExternalMessageId;
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send outbound {ChannelType} message for conversation {ConversationId}", channelType, conversation.Id);
            return (false, $"{channelType} send failed: {ex.Message}");
        }
    }

    private async Task<(bool Success, string? FailureReason)?> TrySendSmsAsync(Guid organizationId, Conversation conversation, Message message, string body, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(conversation.CustomerId, organizationId, cancellationToken);
        if (customer is null || string.IsNullOrWhiteSpace(customer.Phone))
        {
            return (false, "This customer has no phone number on file.");
        }

        var channelAccounts = await _channelAccountRepository.GetAllAsync(organizationId, cancellationToken);
        var account = conversation.ChannelAccountId is Guid channelAccountId
            ? channelAccounts.FirstOrDefault(a => a.Id == channelAccountId && a.ChannelType == "Sms" && a.Status == "Active"
                && !string.IsNullOrWhiteSpace(a.ExternalAccountId) && !string.IsNullOrWhiteSpace(a.AccessToken) && !string.IsNullOrWhiteSpace(a.RefreshToken))
            : channelAccounts.FirstOrDefault(a => a.ChannelType == "Sms" && a.Status == "Active"
                && !string.IsNullOrWhiteSpace(a.ExternalAccountId) && !string.IsNullOrWhiteSpace(a.AccessToken) && !string.IsNullOrWhiteSpace(a.RefreshToken));
        if (account is null)
        {
            return (false, "No active SMS number is connected. Connect one under Settings → Channels.");
        }

        try
        {
            var result = await _smsSender.SendAsync(account.RefreshToken!, account.AccessToken!, account.ExternalAccountId!, customer.Phone, body, cancellationToken);
            message.ExternalMessageId = result.ExternalMessageId;
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send outbound SMS for conversation {ConversationId}", conversation.Id);
            return (false, $"SMS send failed: {ex.Message}");
        }
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
