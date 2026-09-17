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
[Route("api/admin/teams")]
public sealed class AdminTeamsController : ControllerBase
{
    private readonly ITeamRepository _teamRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public AdminTeamsController(
        ITeamRepository teamRepository,
        IDepartmentRepository departmentRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository)
    {
        _teamRepository = teamRepository;
        _departmentRepository = departmentRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminTeamView>>>> GetAll(CancellationToken cancellationToken)
    {
        var teams = await _teamRepository.GetAllAsync(cancellationToken);
        var organizations = await _organizationRepository.GetAllAsync(cancellationToken);
        var departments = await _departmentRepository.GetAllAsync(cancellationToken);
        var users = await _userRepository.GetAllAsync(cancellationToken);

        var organizationNames = organizations.ToDictionary(o => o.Id, o => o.Name);
        var departmentNames = departments.ToDictionary(d => d.Id, d => d.Name);
        var userNames = users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        var result = teams
            .Select(t => new AdminTeamView(
                t.Id,
                t.OrganizationId,
                organizationNames.GetValueOrDefault(t.OrganizationId, "Unknown"),
                t.DepartmentId,
                departmentNames.GetValueOrDefault(t.DepartmentId, "Unknown"),
                t.Name,
                t.Description,
                t.LeaderId,
                t.LeaderId is Guid leaderId ? userNames.GetValueOrDefault(leaderId, "Unknown") : null,
                t.IsActive,
                t.CreatedAt))
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<AdminTeamView>>.Ok(result, "Teams retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] AdminCreateTeamRequest request, CancellationToken cancellationToken)
    {
        var organization = await _organizationRepository.GetByIdAsync(request.OrganizationId, cancellationToken);
        if (organization is null) return BadRequest(ApiResponse<Guid>.Fail("Organization not found."));

        var department = await _departmentRepository.GetByIdAsync(request.DepartmentId, request.OrganizationId, cancellationToken);
        if (department is null) return BadRequest(ApiResponse<Guid>.Fail("Department not found in this organization."));

        var team = new Team
        {
            OrganizationId = request.OrganizationId,
            DepartmentId = request.DepartmentId,
            Name = request.Name,
            Description = request.Description,
            LeaderId = request.LeaderId,
            IsActive = request.IsActive
        };

        var id = await _teamRepository.CreateAsync(team, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, request.OrganizationId, "AdminCreate", "Team", id, cancellationToken, $"Created team {request.Name}");
        return Ok(ApiResponse<Guid>.Ok(id, "Team created successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] AdminUpdateTeamRequest request, CancellationToken cancellationToken)
    {
        var team = await _teamRepository.GetByIdAsync(id, cancellationToken);
        if (team is null) return NotFound(ApiResponse.Fail("Team not found."));

        var department = await _departmentRepository.GetByIdAsync(request.DepartmentId, team.OrganizationId, cancellationToken);
        if (department is null) return BadRequest(ApiResponse.Fail("Department not found in this organization."));

        team.DepartmentId = request.DepartmentId;
        team.Name = request.Name;
        team.Description = request.Description;
        team.LeaderId = request.LeaderId;
        team.IsActive = request.IsActive;

        await _teamRepository.UpdateAsync(team, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, team.OrganizationId, "AdminUpdate", "Team", id, cancellationToken, $"Updated team {request.Name}");
        return Ok(ApiResponse.Ok("Team updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var team = await _teamRepository.GetByIdAsync(id, cancellationToken);
        if (team is null) return NotFound(ApiResponse.Fail("Team not found."));

        await _teamRepository.DeleteAsync(id, team.OrganizationId, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, team.OrganizationId, "AdminDelete", "Team", id, cancellationToken, $"Deleted team {team.Name}");
        return Ok(ApiResponse.Ok("Team deleted successfully."));
    }
}
