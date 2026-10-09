namespace Omni.Domain.Entities;

/// <summary>One queued WhatsApp template message to one recipient, sent in the background by the dispatcher.</summary>
public sealed class TemplateSend
{
    public const string Queued = "queued";
    public const string Sending = "sending";
    public const string Sent = "sent";
    public const string Failed = "failed";

    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid? ApiKeyId { get; set; }
    public Guid BatchId { get; set; }
    public Guid ChannelAccountId { get; set; }
    public Guid TemplateId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string BodyParametersJson { get; set; } = "[]";
    public string? IdempotencyKey { get; set; }

    /// <summary>queued, sending, sent, delivered, read or failed.</summary>
    public string Status { get; set; } = Queued;
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LockedAt { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? MessageId { get; set; }
    public string? ExternalMessageId { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
