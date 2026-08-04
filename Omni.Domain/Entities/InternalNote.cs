namespace Omni.Domain.Entities;

public sealed class InternalNote
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EditedAt { get; set; }
}
