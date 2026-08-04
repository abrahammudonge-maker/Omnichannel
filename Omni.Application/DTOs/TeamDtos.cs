namespace Omni.Application.DTOs;

public sealed record CreateTeamRequest(Guid DepartmentId, string Name, string? Description, Guid? LeaderId, bool IsActive = true);
public sealed record UpdateTeamRequest(Guid DepartmentId, string Name, string? Description, Guid? LeaderId, bool IsActive = true);
public sealed record TeamDto(Guid Id, Guid OrganizationId, Guid DepartmentId, string Name, string? Description, Guid? LeaderId, bool IsActive, DateTimeOffset CreatedAt);
