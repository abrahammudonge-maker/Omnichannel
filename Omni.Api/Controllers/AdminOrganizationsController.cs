using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Api.Extensions;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequirePlatformSuperAdmin")]
[Route("api/admin/organizations")]
public sealed class AdminOrganizationsController : ControllerBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public AdminOrganizationsController(IOrganizationRepository organizationRepository, IAuditLogRepository auditLogRepository)
    {
        _organizationRepository = organizationRepository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Organization>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _organizationRepository.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Organization>>.Ok(result, "Organizations retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var organization = new Organization
        {
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            Country = request.Country,
            Status = "Active"
        };

        var id = await _organizationRepository.CreateAsync(organization, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, id, "AdminCreate", "Organization", id, cancellationToken, $"Created organization {request.Name}");
        return Ok(ApiResponse<Guid>.Ok(id, "Organization created successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] UpdateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var organization = await _organizationRepository.GetByIdAsync(id, cancellationToken);
        if (organization is null) return NotFound(ApiResponse.Fail("Organization not found."));

        organization.Name = request.Name;
        organization.Email = request.Email;
        organization.Phone = request.Phone;
        organization.Country = request.Country;
        organization.Status = request.Status;

        await _organizationRepository.UpdateAsync(organization, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, id, "AdminUpdate", "Organization", id, cancellationToken, $"Updated organization {request.Name}, status {request.Status}");
        return Ok(ApiResponse.Ok("Organization updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        // Logged before deleting: audit_logs.organizationid has a FK to organizations(id), so it must
        // still exist at the time of the write — logging after the delete would violate that constraint.
        await this.LogAuditAsync(_auditLogRepository, id, "AdminDelete", "Organization", id, cancellationToken);
        await _organizationRepository.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Organization deleted successfully."));
    }
}
