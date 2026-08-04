namespace Omni.Application.DTOs;

public sealed record CreateAuditLogRequest(Guid? UserId, string Action, string Entity, Guid? EntityId, string? IpAddress, string? Metadata);
public sealed record AuditLogDto(Guid Id, Guid OrganizationId, Guid? UserId, string Action, string Entity, Guid? EntityId, string? IpAddress, DateTimeOffset Timestamp, string? Metadata);
