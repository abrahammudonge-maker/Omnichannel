namespace Omni.Application.DTOs;

public sealed record SyncMessageTemplatesResponse(int SyncedCount);
public sealed record SendTemplateMessageRequest(Guid ConversationId, Guid TemplateId, List<string>? BodyParameters = null);
public sealed record CreateMessageTemplateRequest(
    Guid ChannelAccountId,
    string Name,
    string Language,
    string Category,
    string? HeaderText,
    string? BodyText,
    string? FooterText,
    List<string>? QuickReplyButtons = null,
    // AUTHENTICATION-category only: Meta writes the body/footer wording itself for these, so
    // HeaderText/BodyText/FooterText/QuickReplyButtons above are ignored when Category is AUTHENTICATION.
    bool AddSecurityRecommendation = true,
    int? CodeExpirationMinutes = null);
