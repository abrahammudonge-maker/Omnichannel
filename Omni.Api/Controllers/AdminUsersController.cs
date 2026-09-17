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
[Authorize(Policy = "RequirePlatformSuperAdmin")]
[Route("api/admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public AdminUsersController(IUserRepository userRepository, IOrganizationRepository organizationRepository, IAuditLogRepository auditLogRepository)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminUserView>>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        var organizations = await _organizationRepository.GetAllAsync(cancellationToken);
        var organizationNames = organizations.ToDictionary(o => o.Id, o => o.Name);

        var result = users
            .Select(u => new AdminUserView(
                u.Id,
                u.OrganizationId,
                organizationNames.GetValueOrDefault(u.OrganizationId, "Unknown"),
                u.FirstName,
                u.LastName,
                u.Email,
                u.Role.ToString(),
                u.IsActive,
                u.CreatedAt))
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<AdminUserView>>.Ok(result, "Users retrieved successfully."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var existing = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
            return BadRequest(ApiResponse<Guid>.Fail("A user with this email already exists."));

        if (!Enum.TryParse<Role>(request.Role, out var role))
            return BadRequest(ApiResponse<Guid>.Fail("Invalid role."));

        var organization = await _organizationRepository.GetByIdAsync(request.OrganizationId, cancellationToken);
        if (organization is null)
            return BadRequest(ApiResponse<Guid>.Fail("Organization not found."));

        var user = new User
        {
            OrganizationId = request.OrganizationId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role,
            IsActive = true
        };

        var id = await _userRepository.CreateAsync(user, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, request.OrganizationId, "AdminCreate", "User", id, cancellationToken, $"Platform admin created {request.Email} with role {request.Role}");
        return Ok(ApiResponse<Guid>.Ok(id, "User created successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
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
        await this.LogAuditAsync(_auditLogRepository, user.OrganizationId, "AdminUpdate", "User", id, cancellationToken, $"Platform admin updated {request.Email}, role {request.Role}, active {request.IsActive}");
        return Ok(ApiResponse.Ok("User updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (id == GetUserId()) return BadRequest(ApiResponse.Fail("You cannot delete your own account."));

        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null) return NotFound(ApiResponse.Fail("User not found."));

        await _userRepository.DeleteAsync(id, user.OrganizationId, cancellationToken);
        await this.LogAuditAsync(_auditLogRepository, user.OrganizationId, "AdminDelete", "User", id, cancellationToken, $"Platform admin deleted {user.Email}");
        return Ok(ApiResponse.Ok("User deleted successfully."));
    }

    private Guid GetUserId() => Guid.Parse(User.Claims.First(c => c.Type == "UserId").Value);
}
