using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Omni.Api.Extensions;
using Omni.Application.Configuration;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireAgent")]
[Route("api/[controller]")]
public sealed class ChannelAccountsController : ControllerBase
{
    private readonly IChannelAccountRepository _channelAccountRepository;
    private readonly IMetaEmbeddedSignupService _metaEmbeddedSignupService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly MetaSettings _metaSettings;

    public ChannelAccountsController(
        IChannelAccountRepository channelAccountRepository,
        IMetaEmbeddedSignupService metaEmbeddedSignupService,
        IOrganizationRepository organizationRepository,
        IAuditLogRepository auditLogRepository,
        IOptions<MetaSettings> metaSettings)
    {
        _channelAccountRepository = channelAccountRepository;
        _metaEmbeddedSignupService = metaEmbeddedSignupService;
        _organizationRepository = organizationRepository;
        _auditLogRepository = auditLogRepository;
        _metaSettings = metaSettings.Value;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ChannelAccountView>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _channelAccountRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ChannelAccountView>>.Ok(result.Select(ToView).ToList(), "Channel accounts retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ChannelAccountView>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _channelAccountRepository.GetByIdAsync(id, organizationId, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<ChannelAccountView>.Fail("Channel account not found."))
            : Ok(ApiResponse<ChannelAccountView>.Ok(ToView(result), "Channel account retrieved successfully."));
    }

    private static ChannelAccountView ToView(ChannelAccount account) => new(
        account.Id, account.OrganizationId, account.ChannelType, account.DisplayName, account.ExternalAccountId, account.ExternalWabaId,
        account.SmtpHost, account.SmtpPort, account.ImapHost, account.ImapPort, account.Status, account.CreatedAt, account.UpdatedAt);

    [HttpPost]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateChannelAccountRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var account = new ChannelAccount
        {
            OrganizationId = organizationId,
            ChannelType = request.ChannelType,
            DisplayName = request.DisplayName,
            ExternalAccountId = request.ExternalAccountId,
            ExternalWabaId = request.ExternalWabaId,
            AccessToken = request.AccessToken,
            RefreshToken = request.RefreshToken,
            WebhookSecret = request.WebhookSecret,
            SmtpHost = request.SmtpHost,
            SmtpPort = request.SmtpPort,
            ImapHost = request.ImapHost,
            ImapPort = request.ImapPort,
            Status = request.Status
        };

        var id = await _channelAccountRepository.CreateAsync(account, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Create", "ChannelAccount", id, cancellationToken, $"{request.ChannelType}: {request.DisplayName}");
        return Ok(ApiResponse<Guid>.Ok(id, "Channel account created successfully."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] UpdateChannelAccountRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var existing = await _channelAccountRepository.GetByIdAsync(id, organizationId, cancellationToken);
        if (existing is null)
            return NotFound(ApiResponse.Fail("Channel account not found."));

        existing.ChannelType = request.ChannelType;
        existing.DisplayName = request.DisplayName;
        existing.ExternalAccountId = request.ExternalAccountId;
        existing.ExternalWabaId = request.ExternalWabaId;
        // The list view never returns these secrets back to the client, so an edit form can't
        // prefill them — treat a blank value as "leave unchanged" rather than wiping it out.
        existing.AccessToken = string.IsNullOrWhiteSpace(request.AccessToken) ? existing.AccessToken : request.AccessToken;
        existing.RefreshToken = string.IsNullOrWhiteSpace(request.RefreshToken) ? existing.RefreshToken : request.RefreshToken;
        existing.WebhookSecret = string.IsNullOrWhiteSpace(request.WebhookSecret) ? existing.WebhookSecret : request.WebhookSecret;
        existing.SmtpHost = request.SmtpHost;
        existing.SmtpPort = request.SmtpPort;
        existing.ImapHost = request.ImapHost;
        existing.ImapPort = request.ImapPort;
        existing.Status = request.Status;

        await _channelAccountRepository.UpdateAsync(existing, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Update", "ChannelAccount", id, cancellationToken, $"{request.ChannelType}: {request.DisplayName}");
        return Ok(ApiResponse.Ok("Channel account updated successfully."));
    }

    /// <summary>Exchanges the Meta OAuth code and lists every candidate account (WABA phone number, or Page) the login granted access to, without connecting anything yet.</summary>
    [HttpPost("connect-meta/discover")]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse<DiscoverMetaChannelResponse>>> DiscoverMeta([FromBody] DiscoverMetaChannelRequest request, CancellationToken cancellationToken)
    {
        var result = request.ChannelType switch
        {
            "WhatsApp" => await _metaEmbeddedSignupService.DiscoverWhatsAppCandidatesAsync(request.Code, request.RedirectUri, cancellationToken),
            "FacebookMessenger" => await _metaEmbeddedSignupService.DiscoverMessengerCandidatesAsync(request.Code, request.RedirectUri, cancellationToken),
            "Instagram" => await _metaEmbeddedSignupService.DiscoverInstagramCandidatesAsync(request.Code, request.RedirectUri, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported Meta channel type: {request.ChannelType}")
        };

        var dto = new DiscoverMetaChannelResponse(result.SessionId, result.Candidates.Select(c => new MetaSignupCandidateDto(c.Id, c.DisplayName)).ToList());
        return Ok(ApiResponse<DiscoverMetaChannelResponse>.Ok(dto, "Accounts discovered successfully."));
    }

    /// <summary>Finalizes a Discover session for one selected candidate and creates/updates the resulting channel account.</summary>
    [HttpPost("connect-meta/confirm")]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse<Guid>>> ConfirmMeta([FromBody] ConfirmMetaChannelRequest request, CancellationToken cancellationToken)
    {
        Guid organizationId;
        if (User.IsInRole("PlatformSuperAdmin") && request.OrganizationId is Guid targetOrgId)
        {
            if (await _organizationRepository.GetByIdAsync(targetOrgId, cancellationToken) is null)
                return BadRequest(ApiResponse<Guid>.Fail("Organization not found."));
            organizationId = targetOrgId;
        }
        else
        {
            organizationId = GetOrganizationId();
        }

        var result = await _metaEmbeddedSignupService.ConfirmSignupAsync(request.SessionId, request.SelectedId, cancellationToken);

        var existing = await _channelAccountRepository.FindByExternalAccountIdAsync(request.ChannelType, result.ExternalAccountId, cancellationToken);
        if (existing is not null && existing.OrganizationId != organizationId)
        {
            return Conflict(ApiResponse<Guid>.Fail("This account is already connected to another organization on this platform. Disconnect it there first."));
        }

        if (existing is not null)
        {
            existing.DisplayName = result.DisplayName;
            existing.AccessToken = result.AccessToken;
            existing.ExternalWabaId = request.ChannelType == "WhatsApp" ? result.ExternalWabaId : existing.ExternalWabaId;
            existing.WebhookSecret = _metaSettings.WebhookVerifyToken;
            existing.Status = "Active";
            await _channelAccountRepository.UpdateAsync(existing, cancellationToken);
            await this.LogAuditAsync(_auditLogRepository, organizationId, "Connect", "ChannelAccount", existing.Id, cancellationToken, $"Reconnected {request.ChannelType} via Meta: {result.DisplayName}");
            return Ok(ApiResponse<Guid>.Ok(existing.Id, "Channel connected successfully."));
        }

        var account = new ChannelAccount
        {
            OrganizationId = organizationId,
            ChannelType = request.ChannelType,
            DisplayName = result.DisplayName,
            ExternalAccountId = result.ExternalAccountId,
            ExternalWabaId = request.ChannelType == "WhatsApp" ? result.ExternalWabaId : null,
            AccessToken = result.AccessToken,
            WebhookSecret = _metaSettings.WebhookVerifyToken,
            Status = "Active"
        };

        var id = await _channelAccountRepository.CreateAsync(account, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Connect", "ChannelAccount", id, cancellationToken, $"Connected {request.ChannelType} via Meta: {result.DisplayName}");
        return Ok(ApiResponse<Guid>.Ok(id, "Channel connected successfully."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        await _channelAccountRepository.DeleteAsync(id, organizationId, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Delete", "ChannelAccount", id, cancellationToken);
        return Ok(ApiResponse.Ok("Channel account deleted successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
