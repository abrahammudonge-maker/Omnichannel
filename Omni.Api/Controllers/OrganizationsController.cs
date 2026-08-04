using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class OrganizationsController : ControllerBase
{
    private readonly IOrganizationRepository _organizationRepository;

    public OrganizationsController(IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Organization>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _organizationRepository.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<Organization>>.Ok(result, "Organizations retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<Organization>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _organizationRepository.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<Organization>.Fail("Organization not found."))
            : Ok(ApiResponse<Organization>.Ok(result, "Organization retrieved successfully."));
    }
}
