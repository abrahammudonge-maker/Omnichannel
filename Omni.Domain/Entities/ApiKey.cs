namespace Omni.Domain.Entities;

public sealed class ApiKey
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string Scope { get; set; } = "integrations";

    /// <summary>For "whatsapp"/"otp" keys: the WhatsApp number this key sends from. Null falls back to the org's OTP/only number.</summary>
    public Guid? ChannelAccountId { get; set; }

    public static bool IsNumberScope(string? scope) =>
        string.Equals(scope, "whatsapp", StringComparison.OrdinalIgnoreCase) || string.Equals(scope, "otp", StringComparison.OrdinalIgnoreCase);
}
