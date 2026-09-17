namespace Omni.Application.DTOs;

public sealed record AssignConversationRequest(Guid ConversationId, Guid? AssignedTo, Guid? AssignedBy, string? Reason);
public sealed record TransferConversationRequest(Guid ConversationId, Guid? AssignedTo, Guid? AssignedBy, string? Reason);
public sealed record ConversationAssignmentDto(Guid Id, Guid ConversationId, Guid AssignedTo, Guid AssignedBy, DateTimeOffset AssignedAt, string? Reason);
