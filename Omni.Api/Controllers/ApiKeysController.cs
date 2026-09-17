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
    private readonly IAuditLogRepository _auditLogRepository;

    public ApiKeysController(IApiKeyRepository apiKeyRepository, IAuditLogRepository auditLogRepository)
    {
        _apiKeyRepository = apiKeyRepository;
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
        var rawKey = GenerateKey();
        var apiKey = new ApiKey
        {
            OrganizationId = organizationId,
            Name = request.Name,
            KeyHash = ApiKeyAuthenticationHandler.Hash(rawKey),
            KeyPrefix = rawKey[..Math.Min(12, rawKey.Length)]
        };
        var id = await _apiKeyRepository.CreateAsync(apiKey, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Create", "ApiKey", id, cancellationToken, request.Name);

        return Ok(ApiResponse<ApiKeyCreatedResponse>.Ok(
            new ApiKeyCreatedResponse(id, request.Name, rawKey, apiKey.KeyPrefix, apiKey.CreatedAt),
            "API key created. Copy it now — it won't be shown again."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        await _apiKeyRepository.RevokeAsync(id, organizationId, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Revoke", "ApiKey", id, cancellationToken);
        return Ok(ApiResponse.Ok("API key revoked."));
    }

    private static string GenerateKey() =>
        "oc_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "").Replace("/", "").Replace("=", "");

    private static ApiKeyView ToView(ApiKey k) => new(k.Id, k.Name, k.KeyPrefix, k.CreatedAt, k.LastUsedAt, k.RevokedAt);

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
