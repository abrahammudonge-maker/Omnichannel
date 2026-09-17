namespace Omni.Domain.Entities;

public sealed class Attachment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid ConversationId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public Guid? UploadedBy { get; set; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;

    public static string MessageTypeForContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return "Document";
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return "Image";
        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return "Video";
        if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) return "Audio";
        return "Document";
    }
}
