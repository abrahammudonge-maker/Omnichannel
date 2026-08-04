namespace Omni.Application.DTOs;

public sealed record CreateDepartmentRequest(string Name, string? Description, bool IsActive = true);
public sealed record UpdateDepartmentRequest(string Name, string? Description, bool IsActive = true);
public sealed record DepartmentDto(Guid Id, Guid OrganizationId, string Name, string? Description, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
