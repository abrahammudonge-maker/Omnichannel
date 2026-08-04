namespace Omni.Application.DTOs;

public sealed record UpdateConversationStatusRequest(Guid ConversationId, string Status, Guid? ChangedBy, string? Reason);
public sealed record ConversationStatusHistoryDto(Guid Id, Guid ConversationId, string Status, Guid? ChangedBy, DateTimeOffset ChangedAt, string? Reason);
