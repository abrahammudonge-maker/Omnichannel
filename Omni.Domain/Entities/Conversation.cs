using Omni.Domain.Enums;

namespace Omni.Domain.Entities;

public sealed class Conversation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid CustomerId { get; set; }
    public Channel Channel { get; set; }
    public string Status { get; set; } = "Open";
    public Guid? AssignedUserId { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
