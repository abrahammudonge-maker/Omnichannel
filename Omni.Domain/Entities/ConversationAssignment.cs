namespace Omni.Domain.Entities;

public sealed class ConversationAssignment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Guid AssignedTo { get; set; }
    public Guid AssignedBy { get; set; }
    public DateTimeOffset AssignedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
}
