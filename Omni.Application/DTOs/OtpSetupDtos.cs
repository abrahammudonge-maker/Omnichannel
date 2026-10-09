namespace Omni.Application.DTOs;

public sealed record OtpSetupRequest(Guid? TemplateId, Guid? ChannelAccountId, bool CreateKey, string? KeyName);

public sealed record OtpSetupTemplateOption(Guid Id, string Name, string Language);
public sealed record OtpSetupAccountOption(Guid Id, string DisplayName, string? ExternalAccountId);
public sealed record OtpSetupKeyView(Guid Id, string Name, string KeyPrefix, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);
public sealed record OtpSetupCreatedKey(Guid Id, string Name, string Key, string KeyPrefix);

public sealed record OtpSetupStatusView(
    Guid? TemplateId,
    Guid? ChannelAccountId,
    Guid? SuggestedTemplateId,
    IReadOnlyList<OtpSetupTemplateOption> ApprovedTemplates,
    IReadOnlyList<OtpSetupAccountOption> WhatsAppAccounts,
    IReadOnlyList<OtpSetupKeyView> OtpKeys,
    bool Ready,
    IReadOnlyList<string> Issues);

public sealed record OtpSetupResult(Guid? TemplateId, Guid? ChannelAccountId, OtpSetupCreatedKey? CreatedKey);
