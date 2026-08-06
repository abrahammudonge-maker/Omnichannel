using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Omni.Application.Configuration;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ChannelAccountsController : ControllerBase
{
    private readonly IChannelAccountRepository _channelAccountRepository;
    private readonly IMetaEmbeddedSignupService _metaEmbeddedSignupService;
    private readonly MetaSettings _metaSettings;

    public ChannelAccountsController(
        IChannelAccountRepository channelAccountRepository,
        IMetaEmbeddedSignupService metaEmbeddedSignupService,
        IOptions<MetaSettings> metaSettings)
    {
        _channelAccountRepository = channelAccountRepository;
        _metaEmbeddedSignupService = metaEmbeddedSignupService;
        _metaSettings = metaSettings.Value;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ChannelAccount>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _channelAccountRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ChannelAccount>>.Ok(result, "Channel accounts retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ChannelAccount>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _channelAccountRepository.GetByIdAsync(id, organizationId, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<ChannelAccount>.Fail("Channel account not found."))
            : Ok(ApiResponse<ChannelAccount>.Ok(result, "Channel account retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateChannelAccountRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var account = new ChannelAccount
        {
            OrganizationId = organizationId,
            ChannelType = request.ChannelType,
            DisplayName = request.DisplayName,
            ExternalAccountId = request.ExternalAccountId,
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
        return Ok(ApiResponse<Guid>.Ok(id, "Channel account created successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] UpdateChannelAccountRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var existing = await _channelAccountRepository.GetByIdAsync(id, organizationId, cancellationToken);
        if (existing is null)
            return NotFound(ApiResponse.Fail("Channel account not found."));

        existing.ChannelType = request.ChannelType;
        existing.DisplayName = request.DisplayName;
        existing.ExternalAccountId = request.ExternalAccountId;
        existing.AccessToken = request.AccessToken;
        existing.RefreshToken = request.RefreshToken;
        existing.WebhookSecret = request.WebhookSecret;
        existing.SmtpHost = request.SmtpHost;
        existing.SmtpPort = request.SmtpPort;
        existing.ImapHost = request.ImapHost;
        existing.ImapPort = request.ImapPort;
        existing.Status = request.Status;

        await _channelAccountRepository.UpdateAsync(existing, cancellationToken);
        return Ok(ApiResponse.Ok("Channel account updated successfully."));
    }

    /// <summary>Completes a Meta Embedded Signup flow (WhatsApp/Messenger/Instagram) and creates the resulting channel account.</summary>
    [HttpPost("connect-meta")]
    public async Task<ActionResult<ApiResponse<Guid>>> ConnectMeta([FromBody] ConnectMetaChannelRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        var result = request.ChannelType switch
        {
            "WhatsApp" => await _metaEmbeddedSignupService.CompleteWhatsAppSignupAsync(request.Code, cancellationToken),
            "FacebookMessenger" => await _metaEmbeddedSignupService.CompleteMessengerSignupAsync(request.Code, cancellationToken),
            "Instagram" => await _metaEmbeddedSignupService.CompleteInstagramSignupAsync(request.Code, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported Meta channel type: {request.ChannelType}")
        };

        var account = new ChannelAccount
        {
            OrganizationId = organizationId,
            ChannelType = request.ChannelType,
            DisplayName = result.DisplayName,
            ExternalAccountId = result.ExternalAccountId,
            AccessToken = result.AccessToken,
            WebhookSecret = _metaSettings.WebhookVerifyToken,
            Status = "Active"
        };

        var id = await _channelAccountRepository.CreateAsync(account, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(id, "Channel connected successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        await _channelAccountRepository.DeleteAsync(id, organizationId, cancellationToken);
        return Ok(ApiResponse.Ok("Channel account deleted successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
