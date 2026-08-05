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
        var organizationId = GetOrganizationId();
        var own = await _organizationRepository.GetByIdAsync(organizationId, cancellationToken);
        var result = own is null ? Array.Empty<Organization>() : new[] { own };
        return Ok(ApiResponse<IReadOnlyList<Organization>>.Ok(result, "Organizations retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<Organization>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id != GetOrganizationId())
        {
            return NotFound(ApiResponse<Organization>.Fail("Organization not found."));
        }

        var result = await _organizationRepository.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<Organization>.Fail("Organization not found."))
            : Ok(ApiResponse<Organization>.Ok(result, "Organization retrieved successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
