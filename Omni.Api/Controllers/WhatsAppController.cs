using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Omni.Api.Authentication;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Application.Services;
using Omni.Domain.Entities;
using Omni.Domain.ValueObjects;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

/// <summary>
/// Template sends for external systems holding a "whatsapp" API key. The key can only list templates,
/// queue sends and read back their status, and only for the WhatsApp number it's tied to.
/// Sends are validated here, stored, and answered at once; TemplateSendDispatcher delivers them in the
/// background and retries temporary Meta failures. AUTHENTICATION templates are left to /api/otp.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
[Route("api/whatsapp")]
public sealed class WhatsAppController : ControllerBase
{
    private const int MaxRecipientsPerRequest = 100;
    private const int MaxIdempotencyKeyLength = 100;
    private static readonly HashSet<string> FinalStatuses = new(StringComparer.OrdinalIgnoreCase) { "delivered", "read", "failed" };

    private readonly IMessageTemplateRepository _messageTemplateRepository;
    private readonly NumberKeyChannelResolver _channelResolver;
    private readonly ITemplateSendRepository _templateSendRepository;
    private readonly ITemplateSendSignal _templateSendSignal;
    private readonly IWhatsAppTemplateSyncService _templateSyncService;

    public WhatsAppController(
        IMessageTemplateRepository messageTemplateRepository,
        NumberKeyChannelResolver channelResolver,
        ITemplateSendRepository templateSendRepository,
        ITemplateSendSignal templateSendSignal,
        IWhatsAppTemplateSyncService templateSyncService)
    {
        _messageTemplateRepository = messageTemplateRepository;
        _channelResolver = channelResolver;
        _templateSendRepository = templateSendRepository;
        _templateSendSignal = templateSendSignal;
        _templateSyncService = templateSyncService;
    }

    /// <summary>Lists the approved, non-authentication templates of the number this key sends from.</summary>
    [HttpGet("templates")]
    [EnableRateLimiting("integration-reads")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<IntegrationTemplateView>>>> GetTemplates(CancellationToken cancellationToken)
    {
        if (!IsWhatsAppKey()) return Forbidden<IReadOnlyList<IntegrationTemplateView>>();

        var organizationId = GetOrganizationId();
        var (account, error) = await _channelResolver.ResolveAsync(User, organizationId, cancellationToken);
        if (account is null) return BadRequest(ApiResponse<IReadOnlyList<IntegrationTemplateView>>.Fail(error!));

        var views = (await _messageTemplateRepository.GetByChannelAccountIdAsync(account.Id, organizationId, cancellationToken))
            .Where(WhatsAppTemplateRules.IsSendable)
            .Select(t => new IntegrationTemplateView(t.Id, t.Name, t.Language, t.Category, t.BodyText, WhatsAppTemplateRules.CountBodyParameters(t.BodyText)))
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Language)
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<IntegrationTemplateView>>.Ok(views, "Templates retrieved successfully."));
    }

    /// <summary>
    /// Queues an approved template for up to 100 numbers and returns at once with a result per recipient.
    /// Recipients that fail validation are rejected individually; the rest are sent in the background.
    /// </summary>
    [HttpPost("send-template")]
    [EnableRateLimiting("integrations")]
    public async Task<ActionResult<ApiResponse<WhatsAppSendResponse>>> SendTemplate([FromBody] WhatsAppSendRequest request, CancellationToken cancellationToken)
    {
        if (!IsWhatsAppKey()) return Forbidden<WhatsAppSendResponse>();
        if (request.Recipients is null || request.Recipients.Count == 0)
            return BadRequest(ApiResponse<WhatsAppSendResponse>.Fail("Add at least one recipient."));
        if (request.Recipients.Count > MaxRecipientsPerRequest)
            return BadRequest(ApiResponse<WhatsAppSendResponse>.Fail($"Send to at most {MaxRecipientsPerRequest} recipients per request."));
        if (request.TemplateId is null && string.IsNullOrWhiteSpace(request.TemplateName))
            return BadRequest(ApiResponse<WhatsAppSendResponse>.Fail("Set templateId or templateName."));

        var organizationId = GetOrganizationId();
        var (account, accountError) = await _channelResolver.ResolveAsync(User, organizationId, cancellationToken);
        if (account is null) return BadRequest(ApiResponse<WhatsAppSendResponse>.Fail(accountError!));

        var (template, templateError, notFound) = await ResolveTemplateAsync(account, organizationId, request, cancellationToken);
        if (template is null)
        {
            return notFound
                ? NotFound(ApiResponse<WhatsAppSendResponse>.Fail(templateError!))
                : BadRequest(ApiResponse<WhatsAppSendResponse>.Fail(templateError!));
        }

        var batchId = Guid.NewGuid();
        var apiKeyId = Guid.TryParse(User.Claims.FirstOrDefault(c => c.Type == "ApiKeyId")?.Value, out var keyId) ? keyId : (Guid?)null;
        var expectedParameters = WhatsAppTemplateRules.CountBodyParameters(template.BodyText);
        var now = DateTimeOffset.UtcNow;

        // Validate everyone first; rejected recipients get their result in place, valid ones become rows.
        var results = new WhatsAppSendResult?[request.Recipients.Count];
        var toQueue = new List<(int Index, TemplateSend Send)>();
        for (var i = 0; i < request.Recipients.Count; i++)
        {
            var recipient = request.Recipients[i];
            var rawNumber = recipient?.PhoneNumber ?? string.Empty;
            var idempotencyKey = string.IsNullOrWhiteSpace(recipient?.IdempotencyKey) ? null : recipient.IdempotencyKey.Trim();
            var number = WhatsAppNumber.NormalizeForSending(rawNumber);
            var parameters = recipient?.BodyParameters ?? new List<string>();

            var error = number is null ? "Enter a valid WhatsApp number with its country code."
                : idempotencyKey?.Length > MaxIdempotencyKeyLength ? $"idempotencyKey can be at most {MaxIdempotencyKeyLength} characters."
                : WhatsAppTemplateRules.ValidateParameters(parameters, expectedParameters);
            if (error is not null)
            {
                results[i] = new WhatsAppSendResult(null, rawNumber, false, "rejected", idempotencyKey, false, error);
                continue;
            }

            toQueue.Add((i, new TemplateSend
            {
                OrganizationId = organizationId,
                ApiKeyId = apiKeyId,
                BatchId = batchId,
                ChannelAccountId = account.Id,
                TemplateId = template.Id,
                PhoneNumber = number!,
                CustomerName = string.IsNullOrWhiteSpace(recipient!.CustomerName) ? null : recipient.CustomerName.Trim(),
                BodyParametersJson = JsonSerializer.Serialize(parameters),
                IdempotencyKey = idempotencyKey,
                Status = TemplateSend.Queued,
                NextAttemptAt = now,
                CreatedAt = now,
                UpdatedAt = now
            }));
        }

        if (toQueue.Count > 0)
        {
            var stored = await _templateSendRepository.CreateManyAsync(toQueue.Select(q => q.Send).ToList(), cancellationToken);
            for (var j = 0; j < toQueue.Count; j++)
            {
                var (send, replayed) = stored[j];
                results[toQueue[j].Index] = new WhatsAppSendResult(send.Id, send.PhoneNumber, true, send.Status, send.IdempotencyKey, replayed, replayed ? send.Error : null);
            }
            if (stored.Any(s => !s.Replayed))
            {
                _templateSendSignal.Notify();
            }
        }

        var finalResults = results.Select(r => r!).ToList();
        var accepted = finalResults.Count(r => r.Accepted);
        var response = new WhatsAppSendResponse(batchId, template.Id, template.Name, accepted, finalResults.Count - accepted, finalResults);
        var message = accepted == 0 ? "No recipients were accepted." : $"{accepted} message(s) queued for delivery.";
        return StatusCode(StatusCodes.Status202Accepted, ApiResponse<WhatsAppSendResponse>.Ok(response, message));
    }

    /// <summary>Current status of one queued message.</summary>
    [HttpGet("messages/{sendId:guid}")]
    [EnableRateLimiting("integration-reads")]
    public async Task<ActionResult<ApiResponse<WhatsAppSendStatusView>>> GetMessage(Guid sendId, CancellationToken cancellationToken)
    {
        if (!IsWhatsAppKey()) return Forbidden<WhatsAppSendStatusView>();

        var send = await _templateSendRepository.FindByIdAsync(sendId, GetOrganizationId(), cancellationToken);
        if (send is null || !IsOwnSend(send)) return NotFound(ApiResponse<WhatsAppSendStatusView>.Fail("Message not found."));
        return Ok(ApiResponse<WhatsAppSendStatusView>.Ok(ToView(send), "Message status retrieved."));
    }

    /// <summary>Looks a message up by the idempotencyKey it was sent with, e.g. after a timeout left you without the response.</summary>
    [HttpGet("messages")]
    [EnableRateLimiting("integration-reads")]
    public async Task<ActionResult<ApiResponse<WhatsAppSendStatusView>>> FindMessage([FromQuery] string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (!IsWhatsAppKey()) return Forbidden<WhatsAppSendStatusView>();
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return BadRequest(ApiResponse<WhatsAppSendStatusView>.Fail("Pass ?idempotencyKey=..."));

        var send = await _templateSendRepository.FindByIdempotencyKeyAsync(GetOrganizationId(), idempotencyKey.Trim(), cancellationToken);
        if (send is null || !IsOwnSend(send)) return NotFound(ApiResponse<WhatsAppSendStatusView>.Fail("Message not found."));
        return Ok(ApiResponse<WhatsAppSendStatusView>.Ok(ToView(send), "Message status retrieved."));
    }

    /// <summary>Status of every message in one send-template call, with counts per status.</summary>
    [HttpGet("batches/{batchId:guid}")]
    [EnableRateLimiting("integration-reads")]
    public async Task<ActionResult<ApiResponse<WhatsAppBatchView>>> GetBatch(Guid batchId, CancellationToken cancellationToken)
    {
        if (!IsWhatsAppKey()) return Forbidden<WhatsAppBatchView>();

        var sends = (await _templateSendRepository.GetByBatchAsync(batchId, GetOrganizationId(), cancellationToken)).Where(IsOwnSend).ToList();
        if (sends.Count == 0) return NotFound(ApiResponse<WhatsAppBatchView>.Fail("Batch not found."));

        var counts = sends.GroupBy(s => s.Status.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.Count());
        var finished = sends.All(s => s.Status is not (TemplateSend.Queued or TemplateSend.Sending));
        return Ok(ApiResponse<WhatsAppBatchView>.Ok(
            new WhatsAppBatchView(batchId, sends.Count, finished, counts, sends.Select(ToView).ToList()),
            "Batch status retrieved."));
    }

    /// <summary>
    /// Finds the template by id or by name (+ language). If it's missing or not approved yet, refreshes the
    /// number's catalog from Meta once and looks again, so a just-approved template works straight away.
    /// </summary>
    private async Task<(MessageTemplate? Template, string? Error, bool NotFound)> ResolveTemplateAsync(
        ChannelAccount account, Guid organizationId, WhatsAppSendRequest request, CancellationToken cancellationToken)
    {
        var result = await FindTemplateAsync(account, organizationId, request, cancellationToken);
        if (result.Template is null && await _templateSyncService.TrySyncIfStaleAsync(account, cancellationToken))
        {
            result = await FindTemplateAsync(account, organizationId, request, cancellationToken);
        }
        return result;
    }

    private async Task<(MessageTemplate? Template, string? Error, bool NotFound)> FindTemplateAsync(
        ChannelAccount account, Guid organizationId, WhatsAppSendRequest request, CancellationToken cancellationToken)
    {
        var templates = await _messageTemplateRepository.GetByChannelAccountIdAsync(account.Id, organizationId, cancellationToken);

        MessageTemplate? template;
        if (request.TemplateId is not null)
        {
            template = templates.FirstOrDefault(t => t.Id == request.TemplateId);
            if (template is null) return (null, "Template not found for this WhatsApp number.", true);
        }
        else
        {
            var named = templates.Where(t => string.Equals(t.Name, request.TemplateName!.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrWhiteSpace(request.Language))
            {
                named = named.Where(t => string.Equals(t.Language, request.Language.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (named.Count == 0) return (null, $"No template named '{request.TemplateName}'{(string.IsNullOrWhiteSpace(request.Language) ? "" : $" in language '{request.Language}'")} for this WhatsApp number.", true);

            // Prefer the approved one when the name exists in several languages and only one is usable.
            var sendable = named.Where(WhatsAppTemplateRules.IsSendable).ToList();
            if (sendable.Count > 1) return (null, $"'{request.TemplateName}' exists in several languages ({string.Join(", ", sendable.Select(t => t.Language))}). Set language.", false);
            template = sendable.Count == 1 ? sendable[0] : named[0];
        }

        if (WhatsAppTemplateRules.IsAuthentication(template))
            return (null, "Send one-time codes through /api/otp/send, not this endpoint.", false);
        if (!WhatsAppTemplateRules.IsSendable(template))
            return (null, $"This template isn't approved yet (status: {template.Status}).", false);
        return (template, null, false);
    }

    private static WhatsAppSendStatusView ToView(TemplateSend s) => new(
        s.Id, s.BatchId, s.PhoneNumber, s.TemplateId, s.Status, FinalStatuses.Contains(s.Status), s.Attempts, s.Error,
        s.IdempotencyKey, s.ExternalMessageId, s.ConversationId, s.CreatedAt, s.UpdatedAt);

    // Keys in the same organization don't see each other's sends.
    private bool IsOwnSend(TemplateSend send) =>
        send.ApiKeyId is null || string.Equals(send.ApiKeyId.ToString(), User.Claims.FirstOrDefault(c => c.Type == "ApiKeyId")?.Value, StringComparison.OrdinalIgnoreCase);

    private ObjectResult Forbidden<T>() =>
        StatusCode(StatusCodes.Status403Forbidden, ApiResponse<T>.Fail("This API key isn't a WhatsApp key."));

    private bool IsWhatsAppKey() => string.Equals(User.Claims.FirstOrDefault(c => c.Type == "Scope")?.Value, "whatsapp", StringComparison.OrdinalIgnoreCase);

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
