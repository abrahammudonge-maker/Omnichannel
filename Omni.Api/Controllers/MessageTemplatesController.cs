using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireAgent")]
[Route("api/[controller]")]
public sealed class MessageTemplatesController : ControllerBase
{
    private readonly IMessageTemplateRepository _messageTemplateRepository;
    private readonly IChannelAccountRepository _channelAccountRepository;
    private readonly IMetaTemplateService _metaTemplateService;

    public MessageTemplatesController(
        IMessageTemplateRepository messageTemplateRepository,
        IChannelAccountRepository channelAccountRepository,
        IMetaTemplateService metaTemplateService)
    {
        _messageTemplateRepository = messageTemplateRepository;
        _channelAccountRepository = channelAccountRepository;
        _metaTemplateService = metaTemplateService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MessageTemplate>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _messageTemplateRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<MessageTemplate>>.Ok(result, "Templates retrieved successfully."));
    }

    [HttpGet("channel/{channelAccountId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MessageTemplate>>>> GetByChannelAccount(Guid channelAccountId, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _messageTemplateRepository.GetByChannelAccountIdAsync(channelAccountId, organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<MessageTemplate>>.Ok(result, "Templates retrieved successfully."));
    }

    /// <summary>Designs a new template, submits it to Meta for review, and caches it locally as Pending.</summary>
    [HttpPost]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateMessageTemplateRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var account = await _channelAccountRepository.GetByIdAsync(request.ChannelAccountId, organizationId, cancellationToken);
        if (account is null)
        {
            return NotFound(ApiResponse<Guid>.Fail("Channel account not found."));
        }

        if (account.ChannelType != "WhatsApp")
        {
            return BadRequest(ApiResponse<Guid>.Fail("Message templates are only supported for WhatsApp channels."));
        }

        if (string.IsNullOrWhiteSpace(account.ExternalWabaId) || string.IsNullOrWhiteSpace(account.AccessToken))
        {
            return BadRequest(ApiResponse<Guid>.Fail(
                "This channel is missing its WhatsApp Business Account ID or access token. Reconnect it via \"Connect via Meta\", or set the WABA ID under \"Enter details manually\", before designing templates."));
        }

        var isAuthentication = string.Equals(request.Category, "AUTHENTICATION", StringComparison.OrdinalIgnoreCase);
        if (!isAuthentication && string.IsNullOrWhiteSpace(request.BodyText))
        {
            return BadRequest(ApiResponse<Guid>.Fail("Body text is required."));
        }

        string componentsJson;
        string bodyText;
        try
        {
            if (isAuthentication)
            {
                componentsJson = await _metaTemplateService.CreateAuthenticationTemplateAsync(
                    account.ExternalWabaId, account.AccessToken!, request.Name, request.Language,
                    request.AddSecurityRecommendation, request.CodeExpirationMinutes, cancellationToken);
                bodyText = "{{1}} is your verification code."
                    + (request.AddSecurityRecommendation ? " For your security, do not share this code." : string.Empty)
                    + (request.CodeExpirationMinutes is int minutes ? $" This code expires in {minutes} minutes." : string.Empty);
            }
            else
            {
                componentsJson = await _metaTemplateService.CreateTemplateAsync(
                    account.ExternalWabaId, account.AccessToken!, request.Name, request.Language, request.Category,
                    request.HeaderText, request.BodyText!, request.FooterText, request.QuickReplyButtons, cancellationToken);
                bodyText = request.BodyText!;
            }
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<Guid>.Fail(ex.Message));
        }

        var template = new MessageTemplate
        {
            OrganizationId = organizationId,
            ChannelAccountId = account.Id,
            Name = request.Name,
            Language = request.Language,
            Category = request.Category,
            Status = "PENDING",
            BodyText = bodyText,
            ComponentsJson = componentsJson
        };
        await _messageTemplateRepository.UpsertAsync(template, cancellationToken);

        return Ok(ApiResponse<Guid>.Ok(template.Id, "Template submitted to Meta for review."));
    }

    /// <summary>Pulls the current template catalog from Meta for this WhatsApp channel and upserts it locally.</summary>
    [HttpPost("sync/{channelAccountId:guid}")]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse<SyncMessageTemplatesResponse>>> Sync(Guid channelAccountId, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var account = await _channelAccountRepository.GetByIdAsync(channelAccountId, organizationId, cancellationToken);
        if (account is null)
        {
            return NotFound(ApiResponse<SyncMessageTemplatesResponse>.Fail("Channel account not found."));
        }

        if (account.ChannelType != "WhatsApp")
        {
            return BadRequest(ApiResponse<SyncMessageTemplatesResponse>.Fail("Message templates are only supported for WhatsApp channels."));
        }

        if (string.IsNullOrWhiteSpace(account.ExternalWabaId) || string.IsNullOrWhiteSpace(account.AccessToken))
        {
            return BadRequest(ApiResponse<SyncMessageTemplatesResponse>.Fail(
                "This channel is missing its WhatsApp Business Account ID or access token. Reconnect it via \"Connect via Meta\", or set the WABA ID under \"Enter details manually\", before syncing templates."));
        }

        var templates = await _metaTemplateService.FetchTemplatesAsync(account.ExternalWabaId, account.AccessToken, cancellationToken);
        foreach (var template in templates)
        {
            await _messageTemplateRepository.UpsertAsync(new MessageTemplate
            {
                OrganizationId = organizationId,
                ChannelAccountId = account.Id,
                Name = template.Name,
                Language = template.Language,
                Category = template.Category,
                Status = template.Status,
                BodyText = template.BodyText,
                ComponentsJson = template.ComponentsJson
            }, cancellationToken);
        }

        return Ok(ApiResponse<SyncMessageTemplatesResponse>.Ok(new SyncMessageTemplatesResponse(templates.Count), $"Synced {templates.Count} template(s)."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
