namespace Omni.Application.DTOs;

public sealed record CreateNotificationRequest(Guid UserId, string Title, string Message);
public sealed record NotificationDto(Guid Id, Guid OrganizationId, Guid UserId, string Title, string Message, bool IsRead, DateTimeOffset CreatedAt);
