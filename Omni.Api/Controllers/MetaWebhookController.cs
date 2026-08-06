using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Domain.Enums;

namespace Omni.Api.Controllers;

/// <summary>
/// Receives Meta (WhatsApp Cloud API / Messenger Platform / Instagram Messaging) webhook events.
/// Called anonymously by Meta's servers, not by authenticated app users.
/// </summary>
[ApiController]
[Route("api/webhooks/meta")]
public sealed class MetaWebhookController : ControllerBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IChannelAccountRepository _channelAccountRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<MetaWebhookController> _logger;

    public MetaWebhookController(
        IOrganizationRepository organizationRepository,
        IChannelAccountRepository channelAccountRepository,
        ICustomerRepository customerRepository,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        ILogger<MetaWebhookController> logger)
    {
        _organizationRepository = organizationRepository;
        _channelAccountRepository = channelAccountRepository;
        _customerRepository = customerRepository;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>Meta's webhook subscription verification handshake.</summary>
    [HttpGet]
    public async Task<IActionResult> Verify(CancellationToken cancellationToken)
    {
        var mode = Request.Query["hub.mode"].ToString();
        var verifyToken = Request.Query["hub.verify_token"].ToString();
        var challenge = Request.Query["hub.challenge"].ToString();

        if (mode != "subscribe" || string.IsNullOrEmpty(verifyToken))
        {
            return BadRequest();
        }

        var (account, _) = await FindChannelAccountByVerifyTokenAsync(verifyToken, cancellationToken);
        if (account is null)
        {
            _logger.LogWarning("Meta webhook verification failed: no channel account matches the provided verify token.");
            return Unauthorized();
        }

        return Content(challenge, "text/plain");
    }

    /// <summary>Receives inbound message events. Always returns 200 quickly, or Meta retries aggressively.</summary>
    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        try
        {
            if (!payload.TryGetProperty("object", out var objectProp) || !payload.TryGetProperty("entry", out var entries))
            {
                return Ok();
            }

            var objectType = objectProp.GetString();
            foreach (var entry in entries.EnumerateArray())
            {
                switch (objectType)
                {
                    case "page":
                        await ProcessMessengerEntryAsync(entry, "FacebookMessenger", cancellationToken);
                        break;
                    case "instagram":
                        await ProcessMessengerEntryAsync(entry, "Instagram", cancellationToken);
                        break;
                    case "whatsapp_business_account":
                        await ProcessWhatsAppEntryAsync(entry, cancellationToken);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Meta webhook payload.");
        }

        return Ok();
    }

    private async Task ProcessMessengerEntryAsync(JsonElement entry, string channelType, CancellationToken cancellationToken)
    {
        if (!entry.TryGetProperty("id", out var idProp))
        {
            return;
        }

        var platformId = idProp.GetString()!;
        var (account, organizationId) = await FindChannelAccountByPlatformIdAsync(platformId, channelType, cancellationToken);
        if (account is null)
        {
            _logger.LogWarning("No {ChannelType} channel account matches platform id {PlatformId}", channelType, platformId);
            return;
        }

        if (!entry.TryGetProperty("messaging", out var messagingEvents))
        {
            return;
        }

        var contactField = channelType == "Instagram" ? "InstagramId" : "FacebookId";
        var channel = channelType == "Instagram" ? Channel.Instagram : Channel.FacebookMessenger;

        foreach (var evt in messagingEvents.EnumerateArray())
        {
            if (!evt.TryGetProperty("sender", out var sender) || !sender.TryGetProperty("id", out var senderIdProp))
            {
                continue;
            }
            if (!evt.TryGetProperty("message", out var message) || !message.TryGetProperty("text", out var textProp))
            {
                continue; // ignore non-text events (delivery receipts, postbacks, attachments, etc.)
            }

            var senderId = senderIdProp.GetString()!;
            var text = textProp.GetString() ?? string.Empty;

            await IngestInboundMessageAsync(organizationId, contactField, senderId, senderId, channel, text, cancellationToken);
        }
    }

    private async Task ProcessWhatsAppEntryAsync(JsonElement entry, CancellationToken cancellationToken)
    {
        if (!entry.TryGetProperty("changes", out var changes))
        {
            return;
        }

        foreach (var change in changes.EnumerateArray())
        {
            if (!change.TryGetProperty("value", out var value))
            {
                continue;
            }
            if (!value.TryGetProperty("metadata", out var metadata) || !metadata.TryGetProperty("phone_number_id", out var phoneNumberIdProp))
            {
                continue;
            }
            if (!value.TryGetProperty("messages", out var messages))
            {
                continue; // status callbacks (sent/delivered/read) have no "messages" array
            }

            var platformId = phoneNumberIdProp.GetString()!;
            var (account, organizationId) = await FindChannelAccountByPlatformIdAsync(platformId, "WhatsApp", cancellationToken);
            if (account is null)
            {
                _logger.LogWarning("No WhatsApp channel account matches phone number id {PlatformId}", platformId);
                continue;
            }

            var contactNames = new Dictionary<string, string>();
            if (value.TryGetProperty("contacts", out var contacts))
            {
                foreach (var contact in contacts.EnumerateArray())
                {
                    if (contact.TryGetProperty("wa_id", out var waIdProp) &&
                        contact.TryGetProperty("profile", out var profile) &&
                        profile.TryGetProperty("name", out var nameProp))
                    {
                        contactNames[waIdProp.GetString()!] = nameProp.GetString() ?? waIdProp.GetString()!;
                    }
                }
            }

            foreach (var msg in messages.EnumerateArray())
            {
                if (!msg.TryGetProperty("from", out var fromProp) || !msg.TryGetProperty("text", out var textObj) || !textObj.TryGetProperty("body", out var bodyProp))
                {
                    continue; // ignore non-text message types for now
                }

                var from = fromProp.GetString()!;
                var text = bodyProp.GetString() ?? string.Empty;
                var displayName = contactNames.GetValueOrDefault(from, from);

                await IngestInboundMessageAsync(organizationId, "WhatsAppNumber", from, displayName, Channel.WhatsApp, text, cancellationToken);
            }
        }
    }

    private async Task IngestInboundMessageAsync(
        Guid organizationId,
        string contactField,
        string contactValue,
        string displayName,
        Channel channel,
        string body,
        CancellationToken cancellationToken)
    {
        var customer = await FindOrCreateCustomerAsync(organizationId, contactField, contactValue, displayName, cancellationToken);
        var conversation = await FindOrCreateConversationAsync(organizationId, customer.Id, channel, cancellationToken);

        await _messageRepository.CreateAsync(new Message
        {
            OrganizationId = organizationId,
            ConversationId = conversation.Id,
            Direction = "Inbound",
            MessageType = "Text",
            Body = body,
            Status = "Received",
            SentAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        var title = $"New {channel} message from {customer.FullName}";
        var preview = body.Length > 140 ? body[..140] + "…" : body;
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
                Title = title,
                Message = preview,
                IsRead = false
            }, cancellationToken);
        }
    }

    private async Task<Customer> FindOrCreateCustomerAsync(Guid organizationId, string contactField, string contactValue, string? displayName, CancellationToken cancellationToken)
    {
        var customers = await _customerRepository.GetAllAsync(organizationId, cancellationToken);
        var existing = customers.FirstOrDefault(c => contactField switch
        {
            "FacebookId" => string.Equals(c.FacebookId, contactValue, StringComparison.OrdinalIgnoreCase),
            "InstagramId" => string.Equals(c.InstagramId, contactValue, StringComparison.OrdinalIgnoreCase),
            "WhatsAppNumber" => string.Equals(c.WhatsAppNumber, contactValue, StringComparison.OrdinalIgnoreCase),
            _ => false
        });
        if (existing is not null)
        {
            return existing;
        }

        var customer = new Customer
        {
            OrganizationId = organizationId,
            FullName = string.IsNullOrWhiteSpace(displayName) ? contactValue : displayName,
            Phone = string.Empty,
            Email = string.Empty,
            FacebookId = contactField == "FacebookId" ? contactValue : null,
            InstagramId = contactField == "InstagramId" ? contactValue : null,
            WhatsAppNumber = contactField == "WhatsAppNumber" ? contactValue : null
        };
        await _customerRepository.CreateAsync(customer, cancellationToken);
        return customer;
    }

    private async Task<Conversation> FindOrCreateConversationAsync(Guid organizationId, Guid customerId, Channel channel, CancellationToken cancellationToken)
    {
        var conversations = await _conversationRepository.GetAllAsync(organizationId, cancellationToken);
        var open = conversations.FirstOrDefault(c =>
            c.CustomerId == customerId &&
            c.Channel == channel &&
            c.Status is not ("Closed" or "Resolved"));
        if (open is not null)
        {
            return open;
        }

        var conversation = new Conversation
        {
            OrganizationId = organizationId,
            CustomerId = customerId,
            Channel = channel,
            Status = "Open"
        };
        await _conversationRepository.CreateAsync(conversation, cancellationToken);
        return conversation;
    }

    private async Task<(ChannelAccount? Account, Guid OrganizationId)> FindChannelAccountByPlatformIdAsync(string platformId, string channelType, CancellationToken cancellationToken)
    {
        var organizations = await _organizationRepository.GetAllAsync(cancellationToken);
        foreach (var org in organizations)
        {
            var accounts = await _channelAccountRepository.GetAllAsync(org.Id, cancellationToken);
            var match = accounts.FirstOrDefault(a => a.ChannelType == channelType && a.Status == "Active" && a.ExternalAccountId == platformId);
            if (match is not null)
            {
                return (match, org.Id);
            }
        }
        return (null, Guid.Empty);
    }

    private async Task<(ChannelAccount? Account, Guid OrganizationId)> FindChannelAccountByVerifyTokenAsync(string verifyToken, CancellationToken cancellationToken)
    {
        var organizations = await _organizationRepository.GetAllAsync(cancellationToken);
        foreach (var org in organizations)
        {
            var accounts = await _channelAccountRepository.GetAllAsync(org.Id, cancellationToken);
            var match = accounts.FirstOrDefault(a =>
                a.Status == "Active" &&
                a.ChannelType is "WhatsApp" or "FacebookMessenger" or "Instagram" &&
                a.WebhookSecret == verifyToken);
            if (match is not null)
            {
                return (match, org.Id);
            }
        }
        return (null, Guid.Empty);
    }
}
