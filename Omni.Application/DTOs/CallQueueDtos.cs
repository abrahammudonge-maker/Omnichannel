namespace Omni.Application.DTOs;

public sealed record CreateCallQueueRequest(Guid? DepartmentId, string Name, string? Description, string Strategy = "RoundRobin", bool IsActive = true);
public sealed record UpdateCallQueueRequest(Guid? DepartmentId, string Name, string? Description, string Strategy, bool IsActive);
public sealed record CreateCallQueueMemberRequest(Guid UserId, int Priority = 0, bool IsActive = true);
