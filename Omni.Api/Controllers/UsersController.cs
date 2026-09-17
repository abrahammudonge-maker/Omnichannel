using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omni.Api.Extensions;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Domain.Enums;
using Omni.Shared.Responses;

namespace Omni.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireAgent")]
[Route("api/[controller]")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public UsersController(IUserRepository userRepository, IAuditLogRepository auditLogRepository)
    {
        _userRepository = userRepository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TeamMemberView>>>> GetAll(CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _userRepository.GetAllAsync(organizationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TeamMemberView>>.Ok(result.Select(ToView).ToList(), "Users retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<TeamMemberView>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var result = await _userRepository.GetByIdAsync(id, organizationId, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<TeamMemberView>.Fail("User not found."))
            : Ok(ApiResponse<TeamMemberView>.Ok(ToView(result), "User retrieved successfully."));
    }

    private static TeamMemberView ToView(User user) => new(
        user.Id, user.OrganizationId, user.FirstName, user.LastName, user.Email, user.Role.ToString(), user.IsActive, user.CreatedAt);

    [HttpPost]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        var existing = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
        {
            return BadRequest(ApiResponse<Guid>.Fail("A user with this email already exists."));
        }

        if (!Enum.TryParse<Role>(request.Role, out var role))
            return BadRequest(ApiResponse<Guid>.Fail("Invalid role."));

        var user = new User
        {
            OrganizationId = organizationId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role,
            IsActive = true
        };

        var id = await _userRepository.CreateAsync(user, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Create", "User", id, cancellationToken, $"Created {request.Email} with role {request.Role}");
        return Ok(ApiResponse<Guid>.Ok(id, "User created successfully."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var user = await _userRepository.GetByIdAsync(id, organizationId, cancellationToken);
        if (user is null) return NotFound(ApiResponse.Fail("User not found."));

        var duplicate = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (duplicate is not null && duplicate.Id != id) return BadRequest(ApiResponse.Fail("A user with this email already exists."));
        if (!Enum.TryParse<Role>(request.Role, out var role)) return BadRequest(ApiResponse.Fail("Invalid role."));
        if (id == GetUserId() && !request.IsActive) return BadRequest(ApiResponse.Fail("You cannot deactivate your own account."));

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Email = request.Email;
        user.Role = role;
        user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password)) user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        await _userRepository.UpdateAsync(user, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Update", "User", id, cancellationToken, $"Updated {request.Email}, role {request.Role}, active {request.IsActive}");
        return Ok(ApiResponse.Ok("User updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireOrganizationAdmin")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (id == GetUserId()) return BadRequest(ApiResponse.Fail("You cannot delete your own account."));
        var organizationId = GetOrganizationId();
        await _userRepository.DeleteAsync(id, organizationId, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, organizationId, "Delete", "User", id, cancellationToken);
        return Ok(ApiResponse.Ok("User deleted successfully."));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
        return claim is null ? Guid.Empty : Guid.Parse(claim.Value);
    }

    private Guid GetUserId() => Guid.Parse(User.Claims.First(c => c.Type == "UserId").Value);
}
