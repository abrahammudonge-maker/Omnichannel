using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Domain.Enums;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;

    public UsersController(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<User>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _userRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<User>>.Ok(result, "Users retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<User>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _userRepository.GetByIdAsync(id, organizationId, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<User>.Fail("User not found."))
            : Ok(ApiResponse<User>.Ok(result, "User retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        var existing = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
        {
            return BadRequest(ApiResponse<Guid>.Fail("A user with this email already exists."));
        }

        var user = new User
        {
            OrganizationId = organizationId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = Enum.Parse<Role>(request.Role),
            IsActive = true
        };

        var id = await _userRepository.CreateAsync(user, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(id, "User created successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
