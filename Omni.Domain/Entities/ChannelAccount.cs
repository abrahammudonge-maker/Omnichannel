namespace Omni.Domain.Entities;

public sealed class ChannelAccount
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string ChannelType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ExternalAccountId { get; set; }
    /// <summary>WhatsApp Business Account id this phone number belongs to. Templates live at this level, not per number.</summary>
    public string? ExternalWabaId { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? WebhookSecret { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? ImapHost { get; set; }
    public int? ImapPort { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
