using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Domain.Enums;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class MessagesController : ControllerBase
{
    private readonly IMessageRepository _messageRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IChannelAccountRepository _channelAccountRepository;
    private readonly IEmailSender _emailSender;
    private readonly IMetaMessageSender _metaMessageSender;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(
        IMessageRepository messageRepository,
        IConversationRepository conversationRepository,
        ICustomerRepository customerRepository,
        IChannelAccountRepository channelAccountRepository,
        IEmailSender emailSender,
        IMetaMessageSender metaMessageSender,
        ILogger<MessagesController> logger)
    {
        _messageRepository = messageRepository;
        _conversationRepository = conversationRepository;
        _customerRepository = customerRepository;
        _channelAccountRepository = channelAccountRepository;
        _emailSender = emailSender;
        _metaMessageSender = metaMessageSender;
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
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateMessageRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var message = new Message
        {
            OrganizationId = organizationId,
            ConversationId = request.ConversationId,
            Direction = request.Direction,
            MessageType = request.MessageType,
            Body = request.Body,
            AttachmentUrl = request.AttachmentUrl,
            Status = request.Status
        };

        var id = await _messageRepository.CreateAsync(message, cancellationToken);

        if (request.Direction == "Outbound")
        {
            var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, organizationId, cancellationToken);
            var sendResult = conversation?.Channel switch
            {
                Channel.Email => await TrySendEmailAsync(organizationId, conversation, request.Body, cancellationToken),
                Channel.WhatsApp or Channel.FacebookMessenger or Channel.Instagram =>
                    await TrySendMetaMessageAsync(organizationId, conversation!, request.Body, cancellationToken),
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

    private async Task<(bool Success, string? FailureReason)?> TrySendEmailAsync(Guid organizationId, Conversation conversation, string body, CancellationToken cancellationToken)
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
            await _emailSender.SendAsync(
                smtpHost,
                smtpPort.Value,
                emailAccount.ExternalAccountId!,
                emailAccount.AccessToken!,
                emailAccount.DisplayName,
                customer.Email,
                "Re: your conversation with us",
                body,
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

    private async Task<(bool Success, string? FailureReason)?> TrySendMetaMessageAsync(Guid organizationId, Conversation conversation, string body, CancellationToken cancellationToken)
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
        var account = channelAccounts.FirstOrDefault(a => a.ChannelType == channelType && a.Status == "Active"
            && !string.IsNullOrWhiteSpace(a.ExternalAccountId) && !string.IsNullOrWhiteSpace(a.AccessToken));
        if (account is null)
        {
            return (false, $"No active {channelType} channel is connected. Connect one under Settings → Channels.");
        }

        try
        {
            await _metaMessageSender.SendAsync(channelType, account.AccessToken!, account.ExternalAccountId!, recipientId, body, cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send outbound {ChannelType} message for conversation {ConversationId}", channelType, conversation.Id);
            return (false, $"{channelType} send failed: {ex.Message}");
        }
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
