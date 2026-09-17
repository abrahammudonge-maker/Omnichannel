using Microsoft.AspNetCore.Mvc;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Domain.Enums;

namespace Omni.Api.Controllers;

/// <summary>
/// Receives inbound SMS webhook events. Called anonymously by the SMS provider, not by authenticated app users.
/// Expects standard Twilio-style form fields (From, To, Body, MessageSid). Authenticated by a per-account secret
/// passed as ?token=... and matched against the channel account's WebhookSecret — provider signature schemes
/// differ (Twilio HMAC vs others), so a shared secret in the URL is used instead of a provider-specific scheme.
/// </summary>
[ApiController]
[Route("api/webhooks/sms")]
public sealed class SmsWebhookController : ControllerBase
{
    private readonly IChannelAccountRepository _channelAccountRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<SmsWebhookController> _logger;

    public SmsWebhookController(
        IChannelAccountRepository channelAccountRepository,
        ICustomerRepository customerRepository,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        ILogger<SmsWebhookController> logger)
    {
        _channelAccountRepository = channelAccountRepository;
        _customerRepository = customerRepository;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive([FromQuery] string? token, CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            return Ok();
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var toNumber = form["To"].ToString();
        var fromNumber = form["From"].ToString();
        var body = form["Body"].ToString();
        var externalMessageId = form["MessageSid"].ToString();

        if (string.IsNullOrWhiteSpace(toNumber) || string.IsNullOrWhiteSpace(fromNumber))
        {
            return Ok();
        }

        var account = await _channelAccountRepository.FindByExternalAccountIdAsync("Sms", toNumber, cancellationToken);
        if (account is null || account.Status != "Active")
        {
            _logger.LogWarning("No active SMS channel account matches number {ToNumber}", toNumber);
            return Ok();
        }

        if (string.IsNullOrWhiteSpace(account.WebhookSecret) || !string.Equals(account.WebhookSecret, token, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        if (!string.IsNullOrWhiteSpace(externalMessageId) &&
            await _messageRepository.ExistsByExternalMessageIdAsync(externalMessageId, account.OrganizationId, cancellationToken))
        {
            return Ok();
        }

        var customer = await FindOrCreateCustomerAsync(account.OrganizationId, fromNumber, cancellationToken);
        var conversation = await FindOrCreateConversationAsync(account.OrganizationId, customer.Id, account.Id, cancellationToken);

        await _messageRepository.CreateAsync(new Message
        {
            OrganizationId = account.OrganizationId,
            ConversationId = conversation.Id,
            ExternalMessageId = string.IsNullOrWhiteSpace(externalMessageId) ? null : externalMessageId,
            Direction = "Inbound",
            MessageType = "Text",
            Body = body,
            Status = "Received",
            SentAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await NotifyAsync(account.OrganizationId, conversation, customer, body, cancellationToken);

        return Ok();
    }

    private async Task<Customer> FindOrCreateCustomerAsync(Guid organizationId, string phone, CancellationToken cancellationToken)
    {
        var customers = await _customerRepository.GetAllAsync(organizationId, cancellationToken);
        var existing = customers.FirstOrDefault(c => string.Equals(c.Phone, phone, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var customer = new Customer
        {
            OrganizationId = organizationId,
            FullName = phone,
            Phone = phone,
            Email = string.Empty
        };
        await _customerRepository.CreateAsync(customer, cancellationToken);
        return customer;
    }

    private async Task<Conversation> FindOrCreateConversationAsync(Guid organizationId, Guid customerId, Guid channelAccountId, CancellationToken cancellationToken)
    {
        var conversations = await _conversationRepository.GetAllAsync(organizationId, cancellationToken);
        var open = conversations.FirstOrDefault(c =>
            c.CustomerId == customerId &&
            c.ChannelAccountId == channelAccountId &&
            c.Channel == Channel.Sms &&
            c.Status is not ("Closed" or "Resolved"));
        if (open is not null)
        {
            return open;
        }

        var conversation = new Conversation
        {
            OrganizationId = organizationId,
            CustomerId = customerId,
            ChannelAccountId = channelAccountId,
            Channel = Channel.Sms,
            Status = "Open"
        };
        await _conversationRepository.CreateAsync(conversation, cancellationToken);
        return conversation;
    }

    private async Task NotifyAsync(Guid organizationId, Conversation conversation, Customer customer, string body, CancellationToken cancellationToken)
    {
        var title = $"New SMS from {customer.FullName}";
        var preview = string.IsNullOrWhiteSpace(body) ? "(no content)" : (body.Length > 140 ? body[..140] + "…" : body);

        var recipientIds = new List<Guid>();
        if (conversation.AssignedUserId is Guid assignedUserId)
        {
            recipientIds.Add(assignedUserId);
        }
        else
        {
            var users = await _userRepository.GetAllAsync(organizationId, cancellationToken);
            recipientIds.AddRange(users.Where(u => u.IsActive).Select(u => u.Id));
        }

        foreach (var userId in recipientIds)
        {
            await _notificationRepository.CreateAsync(new Notification
            {
                OrganizationId = organizationId,
                UserId = userId,
                ConversationId = conversation.Id,
                Title = title,
                Message = preview,
                IsRead = false
            }, cancellationToken);
        }
    }
}
