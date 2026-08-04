namespace Omni.Application.DTOs;

public sealed record UploadAttachmentRequest(Guid ConversationId, string FileName, string ContentType, long FileSize, string StoragePath, Guid UploadedBy);
public sealed record AttachmentDto(Guid Id, Guid ConversationId, string FileName, string ContentType, long FileSize, string StoragePath, Guid UploadedBy, DateTimeOffset UploadedAt);
