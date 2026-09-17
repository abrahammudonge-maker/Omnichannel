using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Omni.Api.Authentication;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Domain.Enums;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

/// <summary>
/// Server-to-server endpoints for external systems (authenticated via API key, not agent login) —
/// e.g. an OTP-generating system that needs to deliver a code to a customer over WhatsApp without
/// ever having an existing conversation to attach the send to.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
[Route("api/integrations")]
public sealed class IntegrationsController : ControllerBase
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    private readonly IMessageTemplateRepository _messageTemplateRepository;
    private readonly IChannelAccountRepository _channelAccountRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly ITemplateMessageService _templateMessageService;
    private readonly IMetaMessageSender _metaMessageSender;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<IntegrationsController> _logger;

    public IntegrationsController(
        IMessageTemplateRepository messageTemplateRepository,
        IChannelAccountRepository channelAccountRepository,
        ICustomerRepository customerRepository,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IAttachmentRepository attachmentRepository,
        ITemplateMessageService templateMessageService,
        IMetaMessageSender metaMessageSender,
        IWebHostEnvironment environment,
        ILogger<IntegrationsController> logger)
    {
        _messageTemplateRepository = messageTemplateRepository;
        _channelAccountRepository = channelAccountRepository;
        _customerRepository = customerRepository;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _attachmentRepository = attachmentRepository;
        _templateMessageService = templateMessageService;
        _metaMessageSender = metaMessageSender;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>Lists this organization's approved WhatsApp templates, for building a template picker in an external UI.</summary>
    [HttpGet("templates")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<IntegrationTemplateView>>>> GetTemplates(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var templates = await _messageTemplateRepository.GetAllAsync(organizationId, cancellationToken);
        var views = templates
            .Where(t => string.Equals(t.Status, "APPROVED", StringComparison.OrdinalIgnoreCase))
            .Select(t => new IntegrationTemplateView(t.Id, t.Name, t.Language, t.Category, t.BodyText, CountBodyParameters(t.BodyText)))
            .OrderBy(t => t.Name)
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<IntegrationTemplateView>>.Ok(views, "Templates retrieved successfully."));
    }

    private static int CountBodyParameters(string bodyText) =>
        System.Text.RegularExpressions.Regex.Matches(bodyText, @"\{\{\d+\}\}").Count;

    /// <summary>Sends an approved WhatsApp template to one or more phone numbers, e.g. an OTP code per customer.</summary>
    [HttpPost("send-template")]
    [EnableRateLimiting("integrations")]
    public async Task<ActionResult<ApiResponse<SendTemplateBulkResponse>>> SendTemplate([FromBody] SendTemplateBulkRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        var template = await _messageTemplateRepository.GetByIdAsync(request.TemplateId, organizationId, cancellationToken);
        if (template is null)
            return NotFound(ApiResponse<SendTemplateBulkResponse>.Fail("Template not found."));
        if (!string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<SendTemplateBulkResponse>.Fail($"This template isn't approved yet (status: {template.Status})."));

        var account = await _channelAccountRepository.GetByIdAsync(template.ChannelAccountId, organizationId, cancellationToken);
        if (account is null || account.Status != "Active" || string.IsNullOrWhiteSpace(account.AccessToken) || string.IsNullOrWhiteSpace(account.ExternalAccountId))
            return BadRequest(ApiResponse<SendTemplateBulkResponse>.Fail("The WhatsApp channel this template belongs to is no longer connected."));

        var results = new List<SendTemplateRecipientResult>();
        foreach (var recipient in request.Recipients)
        {
            if (string.IsNullOrWhiteSpace(recipient.PhoneNumber))
            {
                results.Add(new SendTemplateRecipientResult(recipient.PhoneNumber ?? string.Empty, false, null, "Phone number is required."));
                continue;
            }

            var customer = await FindOrCreateCustomerAsync(organizationId, recipient.PhoneNumber, recipient.CustomerName, cancellationToken);
            var conversation = await FindOrCreateConversationAsync(organizationId, customer.Id, account.Id, cancellationToken);
            var bodyParameters = recipient.BodyParameters ?? new List<string>();

            var sendResult = await _templateMessageService.SendAsync(
                organizationId, conversation.Id, account, template, recipient.PhoneNumber, bodyParameters, cancellationToken);
            results.Add(new SendTemplateRecipientResult(recipient.PhoneNumber, sendResult.Success, sendResult.ExternalMessageId, sendResult.ErrorMessage));
        }

        return Ok(ApiResponse<SendTemplateBulkResponse>.Ok(new SendTemplateBulkResponse(results), "Processed."));
    }

    /// <summary>Lists WhatsApp conversations for this organization, with basic customer info attached, for building an external inbox view.</summary>
    [HttpGet("conversations")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<IntegrationConversationView>>>> GetConversations(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var conversations = await _conversationRepository.GetAllAsync(organizationId, cancellationToken);
        var whatsAppConversations = conversations.Where(c => c.Channel == Channel.WhatsApp).ToList();
        if (whatsAppConversations.Count == 0)
        {
            return Ok(ApiResponse<IReadOnlyList<IntegrationConversationView>>.Ok(Array.Empty<IntegrationConversationView>(), "Conversations retrieved successfully."));
        }

        var customers = (await _customerRepository.GetAllAsync(organizationId, cancellationToken)).ToDictionary(c => c.Id);
        var views = whatsAppConversations
            .Select(c => customers.TryGetValue(c.CustomerId, out var customer)
                ? new IntegrationConversationView(c.Id, c.CustomerId, customer.FullName, customer.WhatsAppNumber, c.Status, c.CreatedAt, c.AssignedUserId)
                : new IntegrationConversationView(c.Id, c.CustomerId, "Unknown", null, c.Status, c.CreatedAt, c.AssignedUserId))
            .OrderByDescending(v => v.CreatedAt)
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<IntegrationConversationView>>.Ok(views, "Conversations retrieved successfully."));
    }

    /// <summary>Lists the messages in one conversation, oldest first — for rendering a thread in an external inbox.</summary>
    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Message>>>> GetConversationMessages(Guid conversationId, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var conversation = await _conversationRepository.GetByIdAsync(conversationId, organizationId, cancellationToken);
        if (conversation is null)
        {
            return NotFound(ApiResponse<IReadOnlyList<Message>>.Fail("Conversation not found."));
        }

        var messages = await _messageRepository.GetByConversationIdAsync(conversationId, organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Message>>.Ok(messages, "Messages retrieved successfully."));
    }

    /// <summary>
    /// Sends a free-text WhatsApp reply. Only works within the 24-hour customer service window (i.e. the
    /// customer messaged within the last 24 hours) — outside that window, use send-template instead.
    /// Finds-or-creates the customer/conversation by phone number, same as send-template.
    /// </summary>
    [HttpPost("messages")]
    [EnableRateLimiting("integrations")]
    public async Task<ActionResult<ApiResponse<SendIntegrationMessageResponse>>> SendMessage([FromBody] SendIntegrationMessageRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(ApiResponse<SendIntegrationMessageResponse>.Fail("phoneNumber and body are required."));
        }

        var resolved = await ResolveOutboundWhatsAppContextAsync(organizationId, request.PhoneNumber, request.CustomerName, cancellationToken);
        if (resolved.ErrorMessage is not null)
        {
            return BadRequest(ApiResponse<SendIntegrationMessageResponse>.Fail(resolved.ErrorMessage));
        }

        var (conversation, account, _) = resolved;

        var message = new Message
        {
            OrganizationId = organizationId,
            ConversationId = conversation!.Id,
            Direction = "Outbound",
            MessageType = "Text",
            Body = request.Body,
            Status = "Queued"
        };
        var messageId = await _messageRepository.CreateAsync(message, cancellationToken);

        try
        {
            var result = await _metaMessageSender.SendAsync("WhatsApp", account!.AccessToken!, account.ExternalAccountId!, request.PhoneNumber, request.Body, null, cancellationToken);
            message.ExternalMessageId = result.ExternalMessageId;
            message.Status = "Sent";
            await _messageRepository.UpdateAsync(message, cancellationToken);
            return Ok(ApiResponse<SendIntegrationMessageResponse>.Ok(
                new SendIntegrationMessageResponse(conversation.Id, messageId, result.ExternalMessageId), "Message sent successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp message via integrations API for conversation {ConversationId}", conversation.Id);
            message.Status = "Failed";
            await _messageRepository.UpdateAsync(message, cancellationToken);
            return Ok(ApiResponse<SendIntegrationMessageResponse>.Fail($"Message saved, but not delivered: {ex.Message}"));
        }
    }

    /// <summary>
    /// Sends a WhatsApp media message (image/video/audio/document) with an optional caption. Subject to
    /// the same 24-hour customer service window as free-text replies. The uploaded file is stored the
    /// same way an agent's attachment is, and the response's attachmentUrl is reachable with this same
    /// API key via GET /api/integrations/attachments/{id}/download.
    /// </summary>
    [HttpPost("messages/media")]
    [EnableRateLimiting("integrations")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<SendIntegrationMessageResponse>>> SendMedia(
        [FromForm] string phoneNumber, [FromForm] string? customerName, [FromForm] string? caption, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return BadRequest(ApiResponse<SendIntegrationMessageResponse>.Fail("phoneNumber is required."));
        }
        if (file is null || file.Length == 0 || file.Length > MaxUploadBytes)
        {
            return BadRequest(ApiResponse<SendIntegrationMessageResponse>.Fail("Choose a file up to 10 MB."));
        }

        var resolved = await ResolveOutboundWhatsAppContextAsync(organizationId, phoneNumber, customerName, cancellationToken);
        if (resolved.ErrorMessage is not null)
        {
            return BadRequest(ApiResponse<SendIntegrationMessageResponse>.Fail(resolved.ErrorMessage));
        }

        var (conversation, account, _) = resolved;
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;

        var attachmentId = Guid.NewGuid();
        var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", organizationId.ToString("N")));
        Directory.CreateDirectory(root);
        var storagePath = Path.Combine(root, attachmentId.ToString("N"));
        await using (var stream = System.IO.File.Create(storagePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var attachment = new Attachment
        {
            Id = attachmentId,
            OrganizationId = organizationId,
            ConversationId = conversation!.Id,
            FileName = Path.GetFileName(file.FileName),
            ContentType = contentType,
            FileSize = file.Length,
            StoragePath = storagePath,
            UploadedBy = null
        };
        await _attachmentRepository.CreateAsync(attachment, cancellationToken);
        var attachmentUrl = $"/api/integrations/attachments/{attachmentId}/download";

        var message = new Message
        {
            OrganizationId = organizationId,
            ConversationId = conversation.Id,
            Direction = "Outbound",
            MessageType = Attachment.MessageTypeForContentType(contentType),
            Body = caption ?? string.Empty,
            AttachmentUrl = attachmentUrl,
            Status = "Queued"
        };
        var messageId = await _messageRepository.CreateAsync(message, cancellationToken);

        try
        {
            await using var sendStream = System.IO.File.OpenRead(storagePath);
            var metaAttachment = new MetaOutboundAttachment(contentType, attachment.FileName, sendStream);
            var result = await _metaMessageSender.SendAsync(
                "WhatsApp", account!.AccessToken!, account.ExternalAccountId!, phoneNumber, caption ?? string.Empty, metaAttachment, cancellationToken);
            message.ExternalMessageId = result.ExternalMessageId;
            message.Status = "Sent";
            await _messageRepository.UpdateAsync(message, cancellationToken);
            return Ok(ApiResponse<SendIntegrationMessageResponse>.Ok(
                new SendIntegrationMessageResponse(conversation.Id, messageId, result.ExternalMessageId, attachmentUrl), "Message sent successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp media message via integrations API for conversation {ConversationId}", conversation.Id);
            message.Status = "Failed";
            await _messageRepository.UpdateAsync(message, cancellationToken);
            return Ok(ApiResponse<SendIntegrationMessageResponse>.Fail($"Message saved, but not delivered: {ex.Message}"));
        }
    }

    /// <summary>
    /// Downloads an attachment (inbound or outbound) belonging to this organization. This mirrors
    /// AttachmentsController's agent-only download endpoint, but accepts the API key instead of an
    /// agent JWT so an external integration can actually reach files it was forwarded or that it sent.
    /// </summary>
    [HttpGet("attachments/{id:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var attachment = await _attachmentRepository.GetByIdAsync(id, organizationId, cancellationToken);
        if (attachment is null || !IsManagedStoragePath(attachment.StoragePath, attachment.OrganizationId) || !System.IO.File.Exists(attachment.StoragePath))
        {
            return NotFound();
        }

        return PhysicalFile(attachment.StoragePath, attachment.ContentType, attachment.FileName);
    }

    private bool IsManagedStoragePath(string storagePath, Guid organizationId)
    {
        var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", organizationId.ToString("N"))) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(storagePath).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A customer may have separate conversations with more than one of this org's WhatsApp numbers
    /// (each number has its own 24h customer-service window with Meta). Resolves whichever one the
    /// customer actually messaged most recently, rather than an arbitrary "first" connected number.
    /// </summary>
    private async Task<(Conversation? Conversation, ChannelAccount? Account, string? ErrorMessage)> ResolveOutboundWhatsAppContextAsync(
        Guid organizationId, string phoneNumber, string? customerName, CancellationToken cancellationToken)
    {
        var channelAccounts = await _channelAccountRepository.GetAllAsync(organizationId, cancellationToken);
        var activeWhatsAppAccounts = channelAccounts.Where(a => a.ChannelType == "WhatsApp" && a.Status == "Active"
            && !string.IsNullOrWhiteSpace(a.ExternalAccountId) && !string.IsNullOrWhiteSpace(a.AccessToken)).ToList();
        if (activeWhatsAppAccounts.Count == 0)
        {
            return (null, null, "No active WhatsApp channel is connected for this organization.");
        }

        var customer = await FindOrCreateCustomerAsync(organizationId, phoneNumber, customerName, cancellationToken);

        var existingConversations = (await _conversationRepository.GetAllAsync(organizationId, cancellationToken))
            .Where(c => c.CustomerId == customer.Id && c.Channel == Channel.WhatsApp)
            .ToList();

        Conversation? conversation = null;
        ChannelAccount? account = null;
        DateTimeOffset? lastInboundSentAt = null;

        foreach (var candidate in existingConversations)
        {
            var candidateAccount = activeWhatsAppAccounts.FirstOrDefault(a => a.Id == candidate.ChannelAccountId);
            if (candidateAccount is null)
            {
                continue;
            }

            var candidateLastInbound = await _messageRepository.GetLastInboundSentAtAsync(candidate.Id, organizationId, cancellationToken);
            if (candidateLastInbound is not null && (lastInboundSentAt is null || candidateLastInbound > lastInboundSentAt))
            {
                conversation = candidate;
                account = candidateAccount;
                lastInboundSentAt = candidateLastInbound;
            }
        }

        if (conversation is null || account is null)
        {
            // No prior inbound message on any connected number — the 24h check below will reject this
            // anyway, but pick a number so the attempt (and its failure) is recorded.
            account = activeWhatsAppAccounts[0];
            conversation = await FindOrCreateConversationAsync(organizationId, customer.Id, account.Id, cancellationToken);
        }

        if (lastInboundSentAt is null || DateTimeOffset.UtcNow - lastInboundSentAt.Value > TimeSpan.FromHours(24))
        {
            return (null, null,
                "This customer hasn't messaged in the last 24 hours (or has never messaged first). Free-text/media replies only work within that window — use send-template to reach them outside it.");
        }

        return (conversation, account, null);
    }

    private async Task<Customer> FindOrCreateCustomerAsync(Guid organizationId, string whatsAppNumber, string? displayName, CancellationToken cancellationToken)
    {
        var customers = await _customerRepository.GetAllAsync(organizationId, cancellationToken);
        var existing = customers.FirstOrDefault(c => string.Equals(c.WhatsAppNumber, whatsAppNumber, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var customer = new Customer
        {
            OrganizationId = organizationId,
            FullName = string.IsNullOrWhiteSpace(displayName) ? whatsAppNumber : displayName,
            Phone = string.Empty,
            Email = string.Empty,
            WhatsAppNumber = whatsAppNumber
        };
        await _customerRepository.CreateAsync(customer, cancellationToken);
        return customer;
    }

    private async Task<Conversation> FindOrCreateConversationAsync(Guid organizationId, Guid customerId, Guid channelAccountId, CancellationToken cancellationToken)
    {
        var conversations = await _conversationRepository.GetAllAsync(organizationId, cancellationToken);
        var existing = conversations.FirstOrDefault(c => c.CustomerId == customerId && c.ChannelAccountId == channelAccountId && c.Channel == Channel.WhatsApp);
        if (existing is not null)
        {
            return existing;
        }

        var conversation = new Conversation
        {
            OrganizationId = organizationId,
            CustomerId = customerId,
            ChannelAccountId = channelAccountId,
            Channel = Channel.WhatsApp,
            Status = "Open"
        };
        await _conversationRepository.CreateAsync(conversation, cancellationToken);
        return conversation;
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
