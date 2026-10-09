using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Api.Authentication;
using Omni.Api.Extensions;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

/// <summary>
/// Admin-facing setup for OTP delivery: picks the approved AUTHENTICATION template and sending number,
/// and issues the OTP API key, so organizations don't have to hand-enter GUIDs or craft API calls.
/// </summary>
[ApiController]
[Authorize(Policy = "RequireOrganizationAdmin")]
[Route("api/otp-setup")]
public sealed class OtpSetupController : ControllerBase
{
    private readonly IOrganizationSettingRepository _settingRepository;
    private readonly IMessageTemplateRepository _messageTemplateRepository;
    private readonly IChannelAccountRepository _channelAccountRepository;
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public OtpSetupController(
        IOrganizationSettingRepository settingRepository,
        IMessageTemplateRepository messageTemplateRepository,
        IChannelAccountRepository channelAccountRepository,
        IApiKeyRepository apiKeyRepository,
        IAuditLogRepository auditLogRepository)
    {
        _settingRepository = settingRepository;
        _messageTemplateRepository = messageTemplateRepository;
        _channelAccountRepository = channelAccountRepository;
        _apiKeyRepository = apiKeyRepository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<OtpSetupStatusView>>> GetStatus(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var status = await BuildStatusAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<OtpSetupStatusView>.Ok(status, "OTP setup status retrieved."));
    }

    [HttpPut]
    public async Task<ActionResult<ApiResponse<OtpSetupResult>>> Apply([FromBody] OtpSetupRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var templates = await _messageTemplateRepository.GetAllAsync(organizationId, cancellationToken);
        var approved = templates.Where(IsOtpTemplate).ToList();

        var settings = await _settingRepository.GetAllAsync(organizationId, cancellationToken);
        var templateId = request.TemplateId ?? ParseSetting(settings, OtpController.TemplateSettingName) ?? SuggestTemplate(approved);
        if (templateId is null || approved.All(t => t.Id != templateId))
        {
            return BadRequest(ApiResponse<OtpSetupResult>.Fail("Choose an approved AUTHENTICATION template first."));
        }

        Guid? channelAccountId = request.ChannelAccountId;
        if (channelAccountId is not null)
        {
            var account = await _channelAccountRepository.GetByIdAsync(channelAccountId.Value, organizationId, cancellationToken);
            if (account is null || account.ChannelType != "WhatsApp" || account.Status != "Active")
            {
                return BadRequest(ApiResponse<OtpSetupResult>.Fail("The selected sending number isn't an active WhatsApp number."));
            }
        }

        await UpsertSettingAsync(organizationId, OtpController.TemplateSettingName, templateId.Value.ToString(), settings, cancellationToken);
        if (channelAccountId is not null)
        {
            await UpsertSettingAsync(organizationId, OtpController.ChannelSettingName, channelAccountId.Value.ToString(), settings, cancellationToken);
        }

        OtpSetupCreatedKey? createdKey = null;
        if (request.CreateKey)
        {
            createdKey = await CreateOtpKeyAsync(organizationId, request.KeyName, cancellationToken);
        }

        var finalChannel = channelAccountId ?? ParseSetting(settings, OtpController.ChannelSettingName);
        return Ok(ApiResponse<OtpSetupResult>.Ok(
            new OtpSetupResult(templateId, finalChannel, createdKey),
            createdKey is null ? "OTP setup saved." : "OTP setup saved. Copy the key now, it won't be shown again."));
    }

    private async Task<OtpSetupStatusView> BuildStatusAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var templates = await _messageTemplateRepository.GetAllAsync(organizationId, cancellationToken);
        var approved = templates.Where(IsOtpTemplate).ToList();
        var accounts = (await _channelAccountRepository.GetAllAsync(organizationId, cancellationToken))
            .Where(a => a.ChannelType == "WhatsApp" && a.Status == "Active")
            .ToList();
        var settings = await _settingRepository.GetAllAsync(organizationId, cancellationToken);
        var keys = (await _apiKeyRepository.GetAllAsync(organizationId, cancellationToken))
            .Where(k => string.Equals(k.Scope, "otp", StringComparison.OrdinalIgnoreCase))
            .Select(k => new OtpSetupKeyView(k.Id, k.Name, k.KeyPrefix, k.CreatedAt, k.RevokedAt))
            .ToList();

        var templateId = ParseSetting(settings, OtpController.TemplateSettingName);
        var channelId = ParseSetting(settings, OtpController.ChannelSettingName);
        var activeKeys = keys.Where(k => k.RevokedAt is null).ToList();

        var issues = new List<string>();
        if (approved.Count == 0) issues.Add("No approved AUTHENTICATION template yet. Create one under Message templates and wait for Meta's approval.");
        else if (templateId is null || approved.All(t => t.Id != templateId)) issues.Add("Pick the approved AUTHENTICATION template for OTP messages.");
        if (accounts.Count == 0) issues.Add("No active WhatsApp number is connected.");
        if (activeKeys.Count == 0) issues.Add("No active OTP key. Create one for the system that generates codes.");

        return new OtpSetupStatusView(
            templateId,
            channelId,
            SuggestTemplate(approved),
            approved.Select(t => new OtpSetupTemplateOption(t.Id, t.Name, t.Language)).ToList(),
            accounts.Select(a => new OtpSetupAccountOption(a.Id, a.DisplayName, a.ExternalAccountId)).ToList(),
            keys,
            Ready: issues.Count == 0,
            Issues: issues);
    }

    private async Task<OtpSetupCreatedKey> CreateOtpKeyAsync(Guid organizationId, string? keyName, CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(keyName) ? "OTP system" : keyName.Trim();
        var rawKey = "oc_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "").Replace("/", "").Replace("=", "");
        var apiKey = new ApiKey
        {
            OrganizationId = organizationId,
            Name = name,
            KeyHash = ApiKeyAuthenticationHandler.Hash(rawKey),
            KeyPrefix = rawKey[..Math.Min(12, rawKey.Length)],
            Scope = "otp"
        };
        var id = await _apiKeyRepository.CreateAsync(apiKey, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Create", "ApiKey", id, cancellationToken, name);
        return new OtpSetupCreatedKey(id, name, rawKey, apiKey.KeyPrefix);
    }

    private async Task UpsertSettingAsync(Guid organizationId, string name, string value, IReadOnlyList<OrganizationSetting> existingSettings, CancellationToken cancellationToken)
    {
        var existing = existingSettings.FirstOrDefault(s => string.Equals(s.SettingName, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.SettingValue = value;
            await _settingRepository.UpdateAsync(existing, cancellationToken);
            return;
        }

        await _settingRepository.CreateAsync(new OrganizationSetting
        {
            OrganizationId = organizationId,
            SettingName = name,
            SettingValue = value
        }, cancellationToken);
    }

    private static bool IsOtpTemplate(MessageTemplate t) =>
        string.Equals(t.Status, "APPROVED", StringComparison.OrdinalIgnoreCase)
        && string.Equals(t.Category, "AUTHENTICATION", StringComparison.OrdinalIgnoreCase);

    // With exactly one approved AUTHENTICATION template there's nothing to choose, so it's suggested.
    private static Guid? SuggestTemplate(IReadOnlyList<MessageTemplate> approved) =>
        approved.Count == 1 ? approved[0].Id : null;

    private static Guid? ParseSetting(IReadOnlyList<OrganizationSetting> settings, string name)
    {
        var value = settings.FirstOrDefault(s => string.Equals(s.SettingName, name, StringComparison.OrdinalIgnoreCase))?.SettingValue;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
