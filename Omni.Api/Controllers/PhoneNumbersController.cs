using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Api.Extensions;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireAgent")]
[Route("api/[controller]")]
public sealed class PhoneNumbersController : ControllerBase
{
    private readonly IPhoneNumberRepository _phoneNumberRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public PhoneNumbersController(IPhoneNumberRepository phoneNumberRepository, IAuditLogRepository auditLogRepository)
    {
        _phoneNumberRepository = phoneNumberRepository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PhoneNumber>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _phoneNumberRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PhoneNumber>>.Ok(result, "Phone numbers retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PhoneNumber>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _phoneNumberRepository.GetByIdAsync(id, organizationId, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<PhoneNumber>.Fail("Phone number not found."))
            : Ok(ApiResponse<PhoneNumber>.Ok(result, "Phone number retrieved successfully."));
    }

    [HttpPost]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreatePhoneNumberRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var phoneNumber = new PhoneNumber
        {
            OrganizationId = organizationId,
            Number = request.Number,
            Provider = request.Provider,
            ProviderNumberId = request.ProviderNumberId,
            DisplayName = request.DisplayName,
            Country = request.Country,
            Status = request.Status
        };
        var id = await _phoneNumberRepository.CreateAsync(phoneNumber, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Create", "PhoneNumber", id, cancellationToken, $"{request.DisplayName}: {request.Number}");
        return Ok(ApiResponse<Guid>.Ok(id, "Phone number created successfully."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] UpdatePhoneNumberRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var existing = await _phoneNumberRepository.GetByIdAsync(id, organizationId, cancellationToken);
        if (existing is null)
        {
            return NotFound(ApiResponse.Fail("Phone number not found."));
        }

        existing.Number = request.Number;
        existing.Provider = request.Provider;
        existing.ProviderNumberId = request.ProviderNumberId;
        existing.DisplayName = request.DisplayName;
        existing.Country = request.Country;
        existing.Status = request.Status;

        await _phoneNumberRepository.UpdateAsync(existing, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Update", "PhoneNumber", id, cancellationToken, $"{request.DisplayName}: {request.Number}");
        return Ok(ApiResponse.Ok("Phone number updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        await _phoneNumberRepository.DeleteAsync(id, organizationId, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Delete", "PhoneNumber", id, cancellationToken);
        return Ok(ApiResponse.Ok("Phone number deleted successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
