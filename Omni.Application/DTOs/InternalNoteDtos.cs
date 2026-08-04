namespace Omni.Application.DTOs;

public sealed record AddInternalNoteRequest(Guid ConversationId, string Body);
public sealed record UpdateInternalNoteRequest(string Body);
public sealed record InternalNoteDto(Guid Id, Guid ConversationId, Guid UserId, string Body, DateTimeOffset CreatedAt, DateTimeOffset? EditedAt);
