namespace Omni.Application.DTOs;

public sealed record CreateChannelAccountRequest(string ChannelType, string DisplayName, string? ExternalAccountId, string? AccessToken, string? RefreshToken, string? WebhookSecret, string? SmtpHost, int? SmtpPort, string? ImapHost, int? ImapPort, string Status = "Active");
public sealed record UpdateChannelAccountRequest(string ChannelType, string DisplayName, string? ExternalAccountId, string? AccessToken, string? RefreshToken, string? WebhookSecret, string? SmtpHost, int? SmtpPort, string? ImapHost, int? ImapPort, string Status = "Active");
public sealed record ChannelAccountDto(Guid Id, Guid OrganizationId, string ChannelType, string DisplayName, string? ExternalAccountId, string? AccessToken, string? RefreshToken, string? WebhookSecret, string? SmtpHost, int? SmtpPort, string? ImapHost, int? ImapPort, string Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
