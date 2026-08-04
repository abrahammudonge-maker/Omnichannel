using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class OrganizationSettingsController : ControllerBase
{
    private readonly IOrganizationSettingRepository _settingRepository;

    public OrganizationSettingsController(IOrganizationSettingRepository settingRepository)
    {
        _settingRepository = settingRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OrganizationSetting>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _settingRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OrganizationSetting>>.Ok(result, "Organization settings retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<OrganizationSetting>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _settingRepository.GetByIdAsync(id, organizationId, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<OrganizationSetting>.Fail("Organization setting not found."))
            : Ok(ApiResponse<OrganizationSetting>.Ok(result, "Organization setting retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateOrganizationSettingRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var setting = new OrganizationSetting
        {
            OrganizationId = organizationId,
            SettingName = request.SettingName,
            SettingValue = request.SettingValue
        };

        var id = await _settingRepository.CreateAsync(setting, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(id, "Organization setting created successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] UpdateOrganizationSettingRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var existing = await _settingRepository.GetByIdAsync(id, organizationId, cancellationToken);
        if (existing is null)
            return NotFound(ApiResponse.Fail("Organization setting not found."));

        existing.SettingName = request.SettingName;
        existing.SettingValue = request.SettingValue;
        await _settingRepository.UpdateAsync(existing, cancellationToken);
        return Ok(ApiResponse.Ok("Organization setting updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        await _settingRepository.DeleteAsync(id, organizationId, cancellationToken);
        return Ok(ApiResponse.Ok("Organization setting deleted successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
