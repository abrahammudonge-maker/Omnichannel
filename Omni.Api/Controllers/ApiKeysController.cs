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

/// <summary>Issues and revokes API keys used by external systems (e.g. an OTP-generating service) to call the integrations API.</summary>
[ApiController]
[Authorize(Policy = "RequireOrganizationAdmin")]
[Route("api/[controller]")]
public sealed class ApiKeysController : ControllerBase
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IChannelAccountRepository _channelAccountRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public ApiKeysController(IApiKeyRepository apiKeyRepository, IChannelAccountRepository channelAccountRepository, IAuditLogRepository auditLogRepository)
    {
        _apiKeyRepository = apiKeyRepository;
        _channelAccountRepository = channelAccountRepository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ApiKeyView>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var keys = await _apiKeyRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ApiKeyView>>.Ok(keys.Select(ToView).ToList(), "API keys retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ApiKeyCreatedResponse>>> Create([FromBody] CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var scope = request.Scope?.Trim().ToLowerInvariant();
        if (scope is null or "") scope = "integrations";
        if (scope is not ("integrations" or "otp" or "whatsapp"))
        {
            return BadRequest(ApiResponse<ApiKeyCreatedResponse>.Fail("Scope must be 'integrations', 'otp' or 'whatsapp'."));
        }

        if (scope == "integrations" && request.ChannelAccountId is not null)
        {
            return BadRequest(ApiResponse<ApiKeyCreatedResponse>.Fail("Only 'otp' and 'whatsapp' keys can be tied to a WhatsApp number."));
        }
        if (scope == "whatsapp" && request.ChannelAccountId is null)
        {
            return BadRequest(ApiResponse<ApiKeyCreatedResponse>.Fail("Choose the WhatsApp number this key sends from."));
        }
        if (request.ChannelAccountId is not null && !await IsUsableAccountAsync(request.ChannelAccountId.Value, organizationId, cancellationToken))
        {
            return BadRequest(ApiResponse<ApiKeyCreatedResponse>.Fail("The selected sending number isn't a connected, active WhatsApp number."));
        }

        var rawKey = GenerateKey();
        var apiKey = new ApiKey
        {
            OrganizationId = organizationId,
            Name = request.Name,
            KeyHash = ApiKeyAuthenticationHandler.Hash(rawKey),
            KeyPrefix = rawKey[..Math.Min(12, rawKey.Length)],
            Scope = scope,
            ChannelAccountId = request.ChannelAccountId
        };
        var id = await _apiKeyRepository.CreateAsync(apiKey, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Create", "ApiKey", id, cancellationToken, request.Name);

        return Ok(ApiResponse<ApiKeyCreatedResponse>.Ok(
            new ApiKeyCreatedResponse(id, request.Name, rawKey, apiKey.KeyPrefix, apiKey.CreatedAt),
            "API key created. Copy it now — it won't be shown again."));
    }

    /// <summary>Ties an otp/whatsapp key to the WhatsApp number it sends from. Null unties it (whatsapp keys must stay tied).</summary>
    [HttpPut("{id:guid}/channel")]
    public async Task<ActionResult<ApiResponse>> SetChannel(Guid id, [FromBody] SetApiKeyChannelRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var key = (await _apiKeyRepository.GetAllAsync(organizationId, cancellationToken)).FirstOrDefault(k => k.Id == id);
        if (key is null || key.RevokedAt is not null)
        {
            return NotFound(ApiResponse.Fail("API key not found or revoked."));
        }
        if (!ApiKey.IsNumberScope(key.Scope))
        {
            return BadRequest(ApiResponse.Fail("Only 'otp' and 'whatsapp' keys can be tied to a WhatsApp number."));
        }
        if (request.ChannelAccountId is null && string.Equals(key.Scope, "whatsapp", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse.Fail("A 'whatsapp' key must stay tied to a WhatsApp number."));
        }
        if (request.ChannelAccountId is not null && !await IsUsableAccountAsync(request.ChannelAccountId.Value, organizationId, cancellationToken))
        {
            return BadRequest(ApiResponse.Fail("The selected sending number isn't a connected, active WhatsApp number."));
        }

        await _apiKeyRepository.SetChannelAccountAsync(id, organizationId, request.ChannelAccountId, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "SetChannel", "ApiKey", id, cancellationToken, request.ChannelAccountId?.ToString());
        return Ok(ApiResponse.Ok(request.ChannelAccountId is null ? "API key untied from its WhatsApp number." : "API key tied to the WhatsApp number."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        await _apiKeyRepository.RevokeAsync(id, organizationId, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Revoke", "ApiKey", id, cancellationToken);
        return Ok(ApiResponse.Ok("API key revoked."));
    }

    internal static string GenerateKey() =>
        "oc_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "").Replace("/", "").Replace("=", "");

    private static ApiKeyView ToView(ApiKey k) => new(k.Id, k.Name, k.KeyPrefix, k.CreatedAt, k.LastUsedAt, k.RevokedAt, k.Scope, k.ChannelAccountId);

    private async Task<bool> IsUsableAccountAsync(Guid channelAccountId, Guid organizationId, CancellationToken cancellationToken)
    {
        var account = await _channelAccountRepository.GetByIdAsync(channelAccountId, organizationId, cancellationToken);
        return account is not null && NumberKeyChannelResolver.IsUsable(account);
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
