namespace Omni.Domain.Entities;

public sealed class OtpMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? MessageId { get; set; }
    public Guid? ChannelAccountId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Purpose { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? ExternalMessageId { get; set; }
    public string Status { get; set; } = "Queued";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
