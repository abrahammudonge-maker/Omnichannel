namespace Omni.Domain.Entities;

public sealed class Call
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? AgentId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderCallId { get; set; }
    public string Direction { get; set; } = "Outbound";
    public string FromNumber { get; set; } = string.Empty;
    public string ToNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Ringing";
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public string? RecordingUrl { get; set; }
    public string RecordingStatus { get; set; } = "NotRecorded";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
