namespace Omni.Application.DTOs;

public sealed record CreateTagRequest(string Name, string? Description, bool IsActive = true);
public sealed record TagDto(Guid Id, Guid OrganizationId, string Name, string? Description, bool IsActive, DateTimeOffset CreatedAt);
