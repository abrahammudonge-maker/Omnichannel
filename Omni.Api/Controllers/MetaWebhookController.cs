using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Omni.Application.Configuration;
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
    private readonly IChannelAccountRepository _channelAccountRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IInboundMessageForwarder _inboundMessageForwarder;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly MetaSettings _metaSettings;
    private readonly ILogger<MetaWebhookController> _logger;

    public MetaWebhookController(
        IChannelAccountRepository channelAccountRepository,
        ICustomerRepository customerRepository,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        IAttachmentRepository attachmentRepository,
        IInboundMessageForwarder inboundMessageForwarder,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment environment,
        IOptions<MetaSettings> metaSettings,
        ILogger<MetaWebhookController> logger)
    {
        _channelAccountRepository = channelAccountRepository;
        _customerRepository = customerRepository;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _attachmentRepository = attachmentRepository;
        _inboundMessageForwarder = inboundMessageForwarder;
        _httpClientFactory = httpClientFactory;
        _environment = environment;
        _metaSettings = metaSettings.Value;
        _logger = logger;
    }

    /// <summary>Meta's webhook subscription verification handshake.</summary>
    [HttpGet]
    public IActionResult Verify()
    {
        var mode = Request.Query["hub.mode"].ToString();
        var verifyToken = Request.Query["hub.verify_token"].ToString();
        var challenge = Request.Query["hub.challenge"].ToString();

        if (mode != "subscribe" || string.IsNullOrEmpty(verifyToken))
        {
            return BadRequest();
        }

        if (string.IsNullOrEmpty(_metaSettings.WebhookVerifyToken) || verifyToken != _metaSettings.WebhookVerifyToken)
        {
            _logger.LogWarning("Meta webhook verification failed: provided verify token did not match the configured Meta:WebhookVerifyToken.");
            return Unauthorized();
        }

        return Content(challenge, "text/plain");
    }

    /// <summary>Receives inbound message events. Always returns 200 quickly, or Meta retries aggressively.</summary>
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        try
        {
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync(cancellationToken);
            Request.Body.Position = 0;

            if (!IsValidSignature(rawBody))
            {
                return Unauthorized();
            }

            using var document = JsonDocument.Parse(rawBody);
            var payload = document.RootElement;
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

    private bool IsValidSignature(string rawBody)
    {
        var signature = Request.Headers["X-Hub-Signature-256"].ToString();
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(_metaSettings.AppSecret) || !signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_metaSettings.AppSecret), Encoding.UTF8.GetBytes(rawBody));
            var provided = Convert.FromHexString(signature[7..]);
            return CryptographicOperations.FixedTimeEquals(expected, provided);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
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
            var senderId = senderIdProp.GetString()!;

            if (evt.TryGetProperty("delivery", out var delivery))
            {
                if (delivery.TryGetProperty("mids", out var mids))
                {
                    foreach (var mid in mids.EnumerateArray())
                    {
                        var deliveredId = mid.GetString();
                        if (!string.IsNullOrWhiteSpace(deliveredId))
                        {
                            await _messageRepository.UpdateStatusByExternalMessageIdAsync(deliveredId, organizationId, "delivered", cancellationToken);
                        }
                    }
                }
                continue;
            }

            if (evt.TryGetProperty("read", out _))
            {
                var existingCustomer = await FindCustomerByContactAsync(organizationId, contactField, senderId, cancellationToken);
                if (existingCustomer is not null)
                {
                    var existingConversation = await FindConversationByCustomerAsync(organizationId, existingCustomer.Id, account.Id, channel, cancellationToken);
                    if (existingConversation is not null)
                    {
                        await _messageRepository.MarkOutboundAsReadAsync(existingConversation.Id, organizationId, cancellationToken);
                    }
                }
                continue;
            }

            if (!evt.TryGetProperty("message", out var message))
            {
                continue;
            }

            var externalMessageId = message.TryGetProperty("mid", out var midProp) ? midProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(externalMessageId) || await _messageRepository.ExistsByExternalMessageIdAsync(externalMessageId, organizationId, cancellationToken))
            {
                continue;
            }

            var text = message.TryGetProperty("text", out var textProp) ? textProp.GetString() : null;

            string? mediaUrl = null;
            if (message.TryGetProperty("attachments", out var attachmentsProp) && attachmentsProp.ValueKind == JsonValueKind.Array && attachmentsProp.GetArrayLength() > 0)
            {
                var firstAttachment = attachmentsProp[0];
                if (firstAttachment.TryGetProperty("payload", out var payloadProp) && payloadProp.TryGetProperty("url", out var urlProp))
                {
                    mediaUrl = urlProp.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(mediaUrl))
            {
                continue; // ignore delivery receipts, postbacks, reactions, etc.
            }

            var profileName = await FetchMessengerProfileNameAsync(senderId, account.AccessToken!, cancellationToken);
            var (customer, conversation) = await ResolveCustomerAndConversationAsync(organizationId, contactField, senderId, profileName ?? senderId, account.Id, channel, cancellationToken);

            if (!string.IsNullOrWhiteSpace(mediaUrl))
            {
                var stored = await DownloadMessengerMediaAsync(mediaUrl, organizationId, conversation.Id, cancellationToken);
                if (stored is null)
                {
                    _logger.LogWarning("Failed to download {ChannelType} media from {Url}", channelType, mediaUrl);
                    continue;
                }
                await CreateInboundMessageAsync(organizationId, customer, conversation, externalMessageId, stored.Value.MessageType, text ?? string.Empty, stored.Value.AttachmentUrl, cancellationToken);
            }
            else
            {
                await CreateInboundMessageAsync(organizationId, customer, conversation, externalMessageId, "Text", text!, null, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Unlike WhatsApp (whose webhook payload includes the customer's profile name for free), Messenger/Instagram
    /// only give us the sender's PSID — the real name has to be fetched separately via the Graph API.
    /// </summary>
    private async Task<string?> FetchMessengerProfileNameAsync(string psid, string pageAccessToken, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GraphApi");
            var response = await client.GetAsync(
                $"https://graph.facebook.com/v21.0/{psid}?fields=name&access_token={Uri.EscapeDataString(pageAccessToken)}",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Messenger profile name for PSID {Psid}", psid);
            return null;
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
            var platformId = phoneNumberIdProp.GetString()!;
            var (account, organizationId) = await FindChannelAccountByPlatformIdAsync(platformId, "WhatsApp", cancellationToken);
            if (account is null)
            {
                _logger.LogWarning("No WhatsApp channel account matches phone number id {PlatformId}", platformId);
                continue;
            }

            if (value.TryGetProperty("statuses", out var statuses))
            {
                foreach (var status in statuses.EnumerateArray())
                {
                    if (status.TryGetProperty("id", out var statusMessageId) && status.TryGetProperty("status", out var statusValue))
                    {
                        var externalMessageId = statusMessageId.GetString();
                        var deliveryStatus = statusValue.GetString();
                        if (!string.IsNullOrWhiteSpace(externalMessageId) && !string.IsNullOrWhiteSpace(deliveryStatus))
                        {
                            await _messageRepository.UpdateStatusByExternalMessageIdAsync(externalMessageId, organizationId, deliveryStatus, cancellationToken);
                            await _inboundMessageForwarder.ForwardStatusUpdateAsync(organizationId, externalMessageId, deliveryStatus, cancellationToken);
                        }
                    }
                }
            }

            if (!value.TryGetProperty("messages", out var messages))
            {
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
                if (!msg.TryGetProperty("id", out var messageIdProp))
                {
                    continue;
                }

                var externalMessageId = messageIdProp.GetString();
                if (string.IsNullOrWhiteSpace(externalMessageId) || await _messageRepository.ExistsByExternalMessageIdAsync(externalMessageId, organizationId, cancellationToken))
                {
                    continue;
                }

                if (!msg.TryGetProperty("from", out var fromProp))
                {
                    continue;
                }

                var from = fromProp.GetString()!;
                var displayName = contactNames.GetValueOrDefault(from, from);
                var whatsAppType = msg.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

                string? body = null;
                string? mediaId = null, mimeType = null, mediaFilename = null, caption = null;

                if (whatsAppType == "text" && msg.TryGetProperty("text", out var textObj) && textObj.TryGetProperty("body", out var bodyProp))
                {
                    body = bodyProp.GetString() ?? string.Empty;
                }
                else if (whatsAppType is "image" or "video" or "audio" or "document" or "sticker"
                    && msg.TryGetProperty(whatsAppType, out var mediaObj) && mediaObj.TryGetProperty("id", out var mediaIdProp))
                {
                    mediaId = mediaIdProp.GetString();
                    mimeType = mediaObj.TryGetProperty("mime_type", out var mimeProp) ? mimeProp.GetString() : null;
                    mediaFilename = mediaObj.TryGetProperty("filename", out var filenameProp) ? filenameProp.GetString() : null;
                    caption = mediaObj.TryGetProperty("caption", out var captionProp) ? captionProp.GetString() : null;
                }
                else
                {
                    continue; // ignore unsupported message types (location, contacts, reactions, interactive, etc.)
                }

                var (customer, conversation) = await ResolveCustomerAndConversationAsync(organizationId, "WhatsAppNumber", from, displayName, account.Id, Channel.WhatsApp, cancellationToken);

                if (mediaId is not null)
                {
                    var stored = await DownloadWhatsAppMediaAsync(mediaId, mimeType, mediaFilename, account.AccessToken!, organizationId, conversation.Id, cancellationToken);
                    if (stored is null)
                    {
                        _logger.LogWarning("Failed to download WhatsApp media {MediaId}", mediaId);
                        continue;
                    }
                    await CreateInboundMessageAsync(organizationId, customer, conversation, externalMessageId, stored.Value.MessageType, caption ?? string.Empty, stored.Value.AttachmentUrl, cancellationToken);
                }
                else
                {
                    await CreateInboundMessageAsync(organizationId, customer, conversation, externalMessageId, "Text", body ?? string.Empty, null, cancellationToken);
                }
            }
        }
    }

    private async Task<(string AttachmentUrl, string MessageType)?> DownloadWhatsAppMediaAsync(
        string mediaId, string? mimeTypeHint, string? filenameHint, string accessToken, Guid organizationId, Guid conversationId, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("GraphApi");

        using var metaRequest = new HttpRequestMessage(HttpMethod.Get, $"https://graph.facebook.com/{_metaSettings.GraphApiVersion}/{mediaId}");
        metaRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var metaResponse = await client.SendAsync(metaRequest, cancellationToken);
        if (!metaResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using var metaDocument = JsonDocument.Parse(await metaResponse.Content.ReadAsStringAsync(cancellationToken));
        if (!metaDocument.RootElement.TryGetProperty("url", out var urlProp))
        {
            return null;
        }
        var mediaUrl = urlProp.GetString();
        if (string.IsNullOrWhiteSpace(mediaUrl))
        {
            return null;
        }
        var mimeType = metaDocument.RootElement.TryGetProperty("mime_type", out var mimeProp) ? mimeProp.GetString() : mimeTypeHint;

        using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, mediaUrl);
        downloadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var downloadResponse = await client.SendAsync(downloadRequest, cancellationToken);
        if (!downloadResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var bytes = await downloadResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        return await StoreAttachmentAsync(organizationId, conversationId, filenameHint, mimeType, bytes, cancellationToken);
    }

    private async Task<(string AttachmentUrl, string MessageType)?> DownloadMessengerMediaAsync(string mediaUrl, Guid organizationId, Guid conversationId, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("GraphApi");
        var response = await client.GetAsync(mediaUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        return await StoreAttachmentAsync(organizationId, conversationId, null, contentType, bytes, cancellationToken);
    }

    private async Task<(string AttachmentUrl, string MessageType)> StoreAttachmentAsync(
        Guid organizationId, Guid conversationId, string? filenameHint, string? contentTypeHint, byte[] bytes, CancellationToken cancellationToken)
    {
        var contentType = string.IsNullOrWhiteSpace(contentTypeHint) ? "application/octet-stream" : contentTypeHint;
        var attachmentId = Guid.NewGuid();
        var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", organizationId.ToString("N")));
        Directory.CreateDirectory(root);
        var storagePath = Path.Combine(root, attachmentId.ToString("N"));
        await System.IO.File.WriteAllBytesAsync(storagePath, bytes, cancellationToken);

        var fileName = string.IsNullOrWhiteSpace(filenameHint) ? $"{attachmentId:N}{ExtensionForContentType(contentType)}" : filenameHint;

        var attachment = new Attachment
        {
            Id = attachmentId,
            OrganizationId = organizationId,
            ConversationId = conversationId,
            FileName = fileName,
            ContentType = contentType,
            FileSize = bytes.LongLength,
            StoragePath = storagePath,
            UploadedBy = null
        };
        await _attachmentRepository.CreateAsync(attachment, cancellationToken);

        return ($"/api/attachments/{attachmentId}/download", Attachment.MessageTypeForContentType(contentType));
    }

    private static string ExtensionForContentType(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "video/mp4" => ".mp4",
        "audio/mpeg" or "audio/mp3" => ".mp3",
        "audio/ogg" => ".ogg",
        "application/pdf" => ".pdf",
        _ => string.Empty
    };

    private async Task<(Customer Customer, Conversation Conversation)> ResolveCustomerAndConversationAsync(
        Guid organizationId, string contactField, string contactValue, string? displayName, Guid? channelAccountId, Channel channel, CancellationToken cancellationToken)
    {
        var customer = await FindOrCreateCustomerAsync(organizationId, contactField, contactValue, displayName, cancellationToken);
        var conversation = await FindOrCreateConversationAsync(organizationId, customer.Id, channelAccountId, channel, cancellationToken);
        return (customer, conversation);
    }

    private async Task CreateInboundMessageAsync(
        Guid organizationId, Customer customer, Conversation conversation, string? externalMessageId, string messageType, string body, string? attachmentUrl, CancellationToken cancellationToken)
    {
        var message = new Message
        {
            OrganizationId = organizationId,
            ConversationId = conversation.Id,
            ExternalMessageId = externalMessageId,
            Direction = "Inbound",
            MessageType = messageType,
            Body = body,
            AttachmentUrl = attachmentUrl,
            Status = "Received",
            SentAt = DateTimeOffset.UtcNow
        };
        await _messageRepository.CreateAsync(message, cancellationToken);
        var publicBaseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        await _inboundMessageForwarder.ForwardAsync(organizationId, conversation, customer, message, publicBaseUrl, cancellationToken);

        var title = $"New {conversation.Channel} message from {customer.FullName}";
        var preview = string.IsNullOrWhiteSpace(body) ? $"[{messageType}]" : (body.Length > 140 ? body[..140] + "…" : body);
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

    /// <summary>Read-only lookup used for delivery/read receipts — never creates a customer, since a receipt for a conversation that doesn't exist yet shouldn't create one.</summary>
    private async Task<Customer?> FindCustomerByContactAsync(Guid organizationId, string contactField, string contactValue, CancellationToken cancellationToken)
    {
        var customers = await _customerRepository.GetAllAsync(organizationId, cancellationToken);
        return customers.FirstOrDefault(c => contactField switch
        {
            "FacebookId" => string.Equals(c.FacebookId, contactValue, StringComparison.OrdinalIgnoreCase),
            "InstagramId" => string.Equals(c.InstagramId, contactValue, StringComparison.OrdinalIgnoreCase),
            "WhatsAppNumber" => string.Equals(c.WhatsAppNumber, contactValue, StringComparison.OrdinalIgnoreCase),
            _ => false
        });
    }

    /// <summary>Read-only counterpart to FindOrCreateConversationAsync, used for delivery/read receipts.</summary>
    private async Task<Conversation?> FindConversationByCustomerAsync(Guid organizationId, Guid customerId, Guid? channelAccountId, Channel channel, CancellationToken cancellationToken)
    {
        var conversations = await _conversationRepository.GetAllAsync(organizationId, cancellationToken);
        return conversations.FirstOrDefault(c => c.CustomerId == customerId && c.ChannelAccountId == channelAccountId && c.Channel == channel);
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
            // Self-heal customers created before we resolved real profile names (e.g. Messenger/Instagram
            // customers whose FullName is still their raw PSID) once a real display name becomes available.
            if (!string.IsNullOrWhiteSpace(displayName) && string.Equals(existing.FullName, contactValue, StringComparison.Ordinal))
            {
                existing.FullName = displayName;
                await _customerRepository.UpdateAsync(existing, cancellationToken);
            }
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

    private async Task<Conversation> FindOrCreateConversationAsync(Guid organizationId, Guid customerId, Guid? channelAccountId, Channel channel, CancellationToken cancellationToken)
    {
        var conversations = await _conversationRepository.GetAllAsync(organizationId, cancellationToken);
        var open = conversations.FirstOrDefault(c =>
            c.CustomerId == customerId &&
            c.ChannelAccountId == channelAccountId &&
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
            ChannelAccountId = channelAccountId,
            Channel = channel,
            Status = "Open"
        };
        await _conversationRepository.CreateAsync(conversation, cancellationToken);
        return conversation;
    }

    private async Task<(ChannelAccount? Account, Guid OrganizationId)> FindChannelAccountByPlatformIdAsync(string platformId, string channelType, CancellationToken cancellationToken)
    {
        var match = await _channelAccountRepository.FindByExternalAccountIdAsync(channelType, platformId, cancellationToken);
        return match is null ? (null, Guid.Empty) : (match, match.OrganizationId);
    }
}
