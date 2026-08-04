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
public sealed class TeamsController : ControllerBase
{
    private readonly ITeamRepository _teamRepository;

    public TeamsController(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Team>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _teamRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Team>>.Ok(result, "Teams retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<Team>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _teamRepository.GetByIdAsync(id, organizationId, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<Team>.Fail("Team not found."))
            : Ok(ApiResponse<Team>.Ok(result, "Team retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var team = new Team
        {
            OrganizationId = organizationId,
            DepartmentId = request.DepartmentId,
            Name = request.Name,
            Description = request.Description,
            LeaderId = request.LeaderId,
            IsActive = request.IsActive
        };

        var id = await _teamRepository.CreateAsync(team, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(id, "Team created successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var existing = await _teamRepository.GetByIdAsync(id, organizationId, cancellationToken);
        if (existing is null)
        {
            return NotFound(ApiResponse.Fail("Team not found."));
        }

        existing.DepartmentId = request.DepartmentId;
        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.LeaderId = request.LeaderId;
        existing.IsActive = request.IsActive;

        await _teamRepository.UpdateAsync(existing, cancellationToken);
        return Ok(ApiResponse.Ok("Team updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        await _teamRepository.DeleteAsync(id, organizationId, cancellationToken);
        return Ok(ApiResponse.Ok("Team deleted successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
