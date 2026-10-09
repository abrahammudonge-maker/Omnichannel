namespace Omni.Application.DTOs;

/// <summary>Pick the template by TemplateId, or by TemplateName (plus Language when the name exists in several languages).</summary>
public sealed record WhatsAppSendRequest(Guid? TemplateId, string? TemplateName, string? Language, List<WhatsAppSendRecipient>? Recipients);

public sealed record WhatsAppSendRecipient(string? PhoneNumber, List<string>? BodyParameters = null, string? CustomerName = null, string? IdempotencyKey = null);

/// <summary>Accepted means queued for sending (or an earlier send replayed by its idempotency key). Status is the row's current status.</summary>
public sealed record WhatsAppSendResult(Guid? SendId, string PhoneNumber, bool Accepted, string Status, string? IdempotencyKey, bool Replayed, string? Error);

public sealed record WhatsAppSendResponse(Guid BatchId, Guid TemplateId, string TemplateName, int Accepted, int Rejected, List<WhatsAppSendResult> Results);

public sealed record WhatsAppSendStatusView(
    Guid SendId,
    Guid BatchId,
    string PhoneNumber,
    Guid TemplateId,
    string Status,
    bool Final,
    int Attempts,
    string? Error,
    string? IdempotencyKey,
    string? ExternalMessageId,
    Guid? ConversationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Finished is true once no message in the batch is still queued or sending.</summary>
public sealed record WhatsAppBatchView(Guid BatchId, int Total, bool Finished, IReadOnlyDictionary<string, int> Counts, List<WhatsAppSendStatusView> Messages);

// Admin setup
public sealed record WhatsAppSetupTemplateOption(Guid Id, string Name, string Language, string Category, int ParameterCount, string BodyText);
public sealed record WhatsAppSetupKeyView(Guid Id, string Name, string KeyPrefix, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt);
public sealed record WhatsAppSetupNumberView(
    Guid ChannelAccountId,
    string DisplayName,
    bool CanSyncTemplates,
    List<WhatsAppSetupTemplateOption> Templates,
    List<WhatsAppSetupKeyView> Keys);
public sealed record WhatsAppSetupStatusView(string BaseUrl, bool Ready, List<string> Issues, List<WhatsAppSetupNumberView> Numbers);
public sealed record CreateWhatsAppKeyRequest(Guid ChannelAccountId, string? Name);
public sealed record WhatsAppKeyCreatedResponse(Guid Id, string Name, string Key, string KeyPrefix, Guid ChannelAccountId, string BaseUrl, List<WhatsAppSetupTemplateOption> Templates);
