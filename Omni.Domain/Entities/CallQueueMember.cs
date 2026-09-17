namespace Omni.Domain.Entities;

public sealed class CallQueueMember
{
    public Guid Id { get; init; } = Guid.NewGuid();
    // Not in the original field list, but section 17's "every call-related table MUST contain
    // OrganizationId" rule wins over that omission — this lets every query filter directly instead
    // of joining through call_queues just to prove tenant ownership.
    public Guid OrganizationId { get; set; }
    public Guid QueueId { get; set; }
    public Guid UserId { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
