namespace Omni.Application.DTOs;

public sealed record SendTemplateRecipient(string PhoneNumber, string? CustomerName, List<string>? BodyParameters);
public sealed record SendTemplateBulkRequest(Guid TemplateId, List<SendTemplateRecipient> Recipients);
public sealed record SendTemplateRecipientResult(string PhoneNumber, bool Success, string? ExternalMessageId, string? ErrorMessage);
public sealed record SendTemplateBulkResponse(List<SendTemplateRecipientResult> Results);

public sealed record IntegrationConversationView(Guid Id, Guid CustomerId, string CustomerName, string? CustomerPhone, string Status, DateTimeOffset CreatedAt, Guid? AssignedUserId);
public sealed record SendIntegrationMessageRequest(string PhoneNumber, string Body, string? CustomerName = null);
public sealed record SendIntegrationMessageResponse(Guid ConversationId, Guid MessageId, string? ExternalMessageId, string? AttachmentUrl = null);

public sealed record IntegrationTemplateView(Guid Id, string Name, string Language, string Category, string BodyText, int ParameterCount);

public sealed record SetIntegrationWebhookRequest(string WebhookUrl, string WebhookSecret);

public sealed record SendIntegrationLocationRequest(string PhoneNumber, double Latitude, double Longitude, string? Name = null, string? Address = null, string? CustomerName = null);
public sealed record SendIntegrationContactRequest(string PhoneNumber, string FormattedName, List<string> PhoneNumbers, string? CustomerName = null);
