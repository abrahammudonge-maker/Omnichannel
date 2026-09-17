namespace Omni.Application.DTOs;

public sealed record ReplaceConversationTagsRequest(IReadOnlyList<Guid> TagIds);
