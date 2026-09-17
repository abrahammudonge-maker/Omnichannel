namespace Omni.Domain.Entities;

public sealed class CallEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid CallId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? ProviderEventId { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
