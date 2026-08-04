namespace Omni.Domain.Entities;

public sealed class ConversationStatusHistory
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid ConversationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
}
