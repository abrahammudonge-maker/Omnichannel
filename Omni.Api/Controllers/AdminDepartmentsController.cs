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
[Route("api/admin/departments")]
public sealed class AdminDepartmentsController : ControllerBase
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public AdminDepartmentsController(IDepartmentRepository departmentRepository, IOrganizationRepository organizationRepository, IAuditLogRepository auditLogRepository)
    {
        _departmentRepository = departmentRepository;
        _organizationRepository = organizationRepository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminDepartmentView>>>> GetAll(CancellationToken cancellationToken)
    {
        var departments = await _departmentRepository.GetAllAsync(cancellationToken);
        var organizations = await _organizationRepository.GetAllAsync(cancellationToken);
        var organizationNames = organizations.ToDictionary(o => o.Id, o => o.Name);

        var result = departments
            .Select(d => new AdminDepartmentView(
                d.Id,
                d.OrganizationId,
                organizationNames.GetValueOrDefault(d.OrganizationId, "Unknown"),
                d.Name,
                d.Description,
                d.IsActive,
                d.CreatedAt))
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<AdminDepartmentView>>.Ok(result, "Departments retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] AdminCreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var organization = await _organizationRepository.GetByIdAsync(request.OrganizationId, cancellationToken);
        if (organization is null) return BadRequest(ApiResponse<Guid>.Fail("Organization not found."));

        var department = new Department
        {
            OrganizationId = request.OrganizationId,
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive
        };

        var id = await _departmentRepository.CreateAsync(department, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, request.OrganizationId, "AdminCreate", "Department", id, cancellationToken, $"Created department {request.Name}");
        return Ok(ApiResponse<Guid>.Ok(id, "Department created successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] AdminUpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await _departmentRepository.GetByIdAsync(id, cancellationToken);
        if (department is null) return NotFound(ApiResponse.Fail("Department not found."));

        department.Name = request.Name;
        department.Description = request.Description;
        department.IsActive = request.IsActive;

        await _departmentRepository.UpdateAsync(department, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, department.OrganizationId, "AdminUpdate", "Department", id, cancellationToken, $"Updated department {request.Name}");
        return Ok(ApiResponse.Ok("Department updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var department = await _departmentRepository.GetByIdAsync(id, cancellationToken);
        if (department is null) return NotFound(ApiResponse.Fail("Department not found."));

        await _departmentRepository.DeleteAsync(id, department.OrganizationId, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, department.OrganizationId, "AdminDelete", "Department", id, cancellationToken, $"Deleted department {department.Name}");
        return Ok(ApiResponse.Ok("Department deleted successfully."));
    }
}
