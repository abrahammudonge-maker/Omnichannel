using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Api.Authentication;
using Omni.Api.Extensions;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Application.Services;
using Omni.Domain.Entities;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

/// <summary>
/// Admin-facing setup for template sends by external systems: shows each connected WhatsApp number with its
/// sendable templates and keys, and issues a key tied to a number in one call, returning everything the
/// external developer needs (key, base URL, template names and parameter counts).
/// </summary>
[ApiController]
[Authorize(Policy = "RequireOrganizationAdmin")]
[Route("api/whatsapp-setup")]
public sealed class WhatsAppSetupController : ControllerBase
{
    private readonly IChannelAccountRepository _channelAccountRepository;
    private readonly IMessageTemplateRepository _messageTemplateRepository;
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IWhatsAppTemplateSyncService _templateSyncService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<WhatsAppSetupController> _logger;

    public WhatsAppSetupController(
        IChannelAccountRepository channelAccountRepository,
        IMessageTemplateRepository messageTemplateRepository,
        IApiKeyRepository apiKeyRepository,
        IWhatsAppTemplateSyncService templateSyncService,
        IAuditLogRepository auditLogRepository,
        ILogger<WhatsAppSetupController> logger)
    {
        _channelAccountRepository = channelAccountRepository;
        _messageTemplateRepository = messageTemplateRepository;
        _apiKeyRepository = apiKeyRepository;
        _templateSyncService = templateSyncService;
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<WhatsAppSetupStatusView>>> GetStatus(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var accounts = await GetUsableAccountsAsync(organizationId, cancellationToken);
        var templates = await _messageTemplateRepository.GetAllAsync(organizationId, cancellationToken);
        var keys = (await _apiKeyRepository.GetAllAsync(organizationId, cancellationToken))
            .Where(k => k.RevokedAt is null && string.Equals(k.Scope, "whatsapp", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var numbers = accounts.Select(a => new WhatsAppSetupNumberView(
            a.Id,
            a.DisplayName,
            _templateSyncService.CanSync(a),
            TemplateOptions(templates.Where(t => t.ChannelAccountId == a.Id)),
            keys.Where(k => k.ChannelAccountId == a.Id).Select(k => new WhatsAppSetupKeyView(k.Id, k.Name, k.KeyPrefix, k.CreatedAt, k.LastUsedAt)).ToList()))
            .ToList();

        var issues = new List<string>();
        if (numbers.Count == 0) issues.Add("No active WhatsApp number is connected.");
        else if (numbers.All(n => n.Templates.Count == 0)) issues.Add("No approved, non-OTP template yet. Create one under Message templates and wait for Meta's approval.");
        if (numbers.Count > 0 && numbers.All(n => n.Keys.Count == 0)) issues.Add("No WhatsApp key yet. Create one for the system that will send messages.");
        foreach (var number in numbers.Where(n => !n.CanSyncTemplates))
        {
            issues.Add($"{number.DisplayName} is missing its WhatsApp Business Account ID, so its templates can't sync automatically. Reconnect it via \"Connect via Meta\".");
        }

        return Ok(ApiResponse<WhatsAppSetupStatusView>.Ok(
            new WhatsAppSetupStatusView(PublicBaseUrl(), issues.Count == 0, issues, numbers),
            "WhatsApp setup status retrieved."));
    }

    /// <summary>Creates a "whatsapp" key tied to the number. The raw key is only returned here.</summary>
    [HttpPost("keys")]
    public async Task<ActionResult<ApiResponse<WhatsAppKeyCreatedResponse>>> CreateKey([FromBody] CreateWhatsAppKeyRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var account = (await GetUsableAccountsAsync(organizationId, cancellationToken)).FirstOrDefault(a => a.Id == request.ChannelAccountId);
        if (account is null)
        {
            return BadRequest(ApiResponse<WhatsAppKeyCreatedResponse>.Fail("The selected sending number isn't a connected, active WhatsApp number."));
        }

        // Fresh catalog so the response lists what the external developer can use right now.
        await _templateSyncService.TrySyncIfStaleAsync(account, cancellationToken);

        var name = string.IsNullOrWhiteSpace(request.Name) ? $"WhatsApp sends ({account.DisplayName})" : request.Name.Trim();
        var rawKey = ApiKeysController.GenerateKey();
        var apiKey = new ApiKey
        {
            OrganizationId = organizationId,
            Name = name,
            KeyHash = ApiKeyAuthenticationHandler.Hash(rawKey),
            KeyPrefix = rawKey[..Math.Min(12, rawKey.Length)],
            Scope = "whatsapp",
            ChannelAccountId = account.Id
        };
        var id = await _apiKeyRepository.CreateAsync(apiKey, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Create", "ApiKey", id, cancellationToken, name);

        var templates = await _messageTemplateRepository.GetByChannelAccountIdAsync(account.Id, organizationId, cancellationToken);
        return Ok(ApiResponse<WhatsAppKeyCreatedResponse>.Ok(
            new WhatsAppKeyCreatedResponse(id, name, rawKey, apiKey.KeyPrefix, account.Id, PublicBaseUrl(), TemplateOptions(templates)),
            "WhatsApp key created. Copy it now, it won't be shown again."));
    }

    /// <summary>Refreshes every connected number's template catalog from Meta now.</summary>
    [HttpPost("sync-templates")]
    public async Task<ActionResult<ApiResponse>> SyncTemplates(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var synced = 0;
        var failed = new List<string>();
        foreach (var account in (await GetUsableAccountsAsync(organizationId, cancellationToken)).Where(_templateSyncService.CanSync))
        {
            try
            {
                synced += await _templateSyncService.SyncAsync(account, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Template sync failed for channel account {ChannelAccountId}.", account.Id);
                failed.Add(account.DisplayName);
            }
        }

        return failed.Count == 0
            ? Ok(ApiResponse.Ok($"Synced {synced} template(s)."))
            : BadRequest(ApiResponse.Fail($"Synced {synced} template(s), but Meta rejected the sync for: {string.Join(", ", failed)}."));
    }

    private async Task<List<ChannelAccount>> GetUsableAccountsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        (await _channelAccountRepository.GetAllAsync(organizationId, cancellationToken)).Where(NumberKeyChannelResolver.IsUsable).ToList();

    private static List<WhatsAppSetupTemplateOption> TemplateOptions(IEnumerable<MessageTemplate> templates) =>
        templates.Where(WhatsAppTemplateRules.IsSendable)
            .OrderBy(t => t.Name).ThenBy(t => t.Language)
            .Select(t => new WhatsAppSetupTemplateOption(t.Id, t.Name, t.Language, t.Category, WhatsAppTemplateRules.CountBodyParameters(t.BodyText), t.BodyText))
            .ToList();

    private string PublicBaseUrl() => $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
