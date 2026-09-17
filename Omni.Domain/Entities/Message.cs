using Omni.Domain.Enums;

namespace Omni.Domain.Entities;

public sealed class Message
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid ConversationId { get; set; }
    public string? ExternalMessageId { get; set; }
    public string Direction { get; set; } = "Inbound";
    public string MessageType { get; set; } = "Text";
    public string Body { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "Sent";
}
