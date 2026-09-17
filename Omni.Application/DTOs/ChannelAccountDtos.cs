namespace Omni.Application.DTOs;

public sealed record CreateChannelAccountRequest(string ChannelType, string DisplayName, string? ExternalAccountId, string? ExternalWabaId, string? AccessToken, string? RefreshToken, string? WebhookSecret, string? SmtpHost, int? SmtpPort, string? ImapHost, int? ImapPort, string Status = "Active");
public sealed record UpdateChannelAccountRequest(string ChannelType, string DisplayName, string? ExternalAccountId, string? ExternalWabaId, string? AccessToken, string? RefreshToken, string? WebhookSecret, string? SmtpHost, int? SmtpPort, string? ImapHost, int? ImapPort, string Status = "Active");
public sealed record ChannelAccountDto(Guid Id, Guid OrganizationId, string ChannelType, string DisplayName, string? ExternalAccountId, string? ExternalWabaId, string? AccessToken, string? RefreshToken, string? WebhookSecret, string? SmtpHost, int? SmtpPort, string? ImapHost, int? ImapPort, string Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
public sealed record DiscoverMetaChannelRequest(string ChannelType, string Code, string RedirectUri);
public sealed record MetaSignupCandidateDto(string Id, string DisplayName);
public sealed record DiscoverMetaChannelResponse(string SessionId, IReadOnlyList<MetaSignupCandidateDto> Candidates);
public sealed record ConfirmMetaChannelRequest(string ChannelType, string SessionId, string SelectedId, Guid? OrganizationId = null);
