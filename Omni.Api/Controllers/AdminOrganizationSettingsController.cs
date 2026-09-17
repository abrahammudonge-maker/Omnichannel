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
[Route("api/admin/organization-settings")]
public sealed class AdminOrganizationSettingsController : ControllerBase
{
    private readonly IOrganizationSettingRepository _organizationSettingRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public AdminOrganizationSettingsController(IOrganizationSettingRepository organizationSettingRepository, IOrganizationRepository organizationRepository, IAuditLogRepository auditLogRepository)
    {
        _organizationSettingRepository = organizationSettingRepository;
        _organizationRepository = organizationRepository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminOrganizationSettingView>>>> GetAll(CancellationToken cancellationToken)
    {
        var settings = await _organizationSettingRepository.GetAllAsync(cancellationToken);
        var organizations = await _organizationRepository.GetAllAsync(cancellationToken);
        var organizationNames = organizations.ToDictionary(o => o.Id, o => o.Name);

        var result = settings
            .Select(s => new AdminOrganizationSettingView(
                s.Id,
                s.OrganizationId,
                organizationNames.GetValueOrDefault(s.OrganizationId, "Unknown"),
                s.SettingName,
                s.SettingValue))
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<AdminOrganizationSettingView>>.Ok(result, "Organization settings retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] AdminCreateOrganizationSettingRequest request, CancellationToken cancellationToken)
    {
        var organization = await _organizationRepository.GetByIdAsync(request.OrganizationId, cancellationToken);
        if (organization is null) return BadRequest(ApiResponse<Guid>.Fail("Organization not found."));

        var setting = new OrganizationSetting
        {
            OrganizationId = request.OrganizationId,
            SettingName = request.SettingName,
            SettingValue = request.SettingValue
        };

        var id = await _organizationSettingRepository.CreateAsync(setting, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, request.OrganizationId, "AdminCreate", "OrganizationSetting", id, cancellationToken, $"Set {request.SettingName}");
        return Ok(ApiResponse<Guid>.Ok(id, "Organization setting created successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] AdminUpdateOrganizationSettingRequest request, CancellationToken cancellationToken)
    {
        var setting = await _organizationSettingRepository.GetByIdAsync(id, cancellationToken);
        if (setting is null) return NotFound(ApiResponse.Fail("Organization setting not found."));

        setting.SettingName = request.SettingName;
        setting.SettingValue = request.SettingValue;

        await _organizationSettingRepository.UpdateAsync(setting, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, setting.OrganizationId, "AdminUpdate", "OrganizationSetting", id, cancellationToken, $"Set {request.SettingName}");
        return Ok(ApiResponse.Ok("Organization setting updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var setting = await _organizationSettingRepository.GetByIdAsync(id, cancellationToken);
        if (setting is null) return NotFound(ApiResponse.Fail("Organization setting not found."));

        await _organizationSettingRepository.DeleteAsync(id, setting.OrganizationId, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, setting.OrganizationId, "AdminDelete", "OrganizationSetting", id, cancellationToken, $"Deleted {setting.SettingName}");
        return Ok(ApiResponse.Ok("Organization setting deleted successfully."));
    }
}
