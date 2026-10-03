namespace Omni.Application.DTOs;

public sealed record BulkTemplateRecipient(string PhoneNumber, string? CustomerName, List<string>? BodyParameters);
public sealed record SendBulkTemplateRequest(Guid TemplateId, List<BulkTemplateRecipient> Recipients);
public sealed record BulkTemplateRecipientResult(string PhoneNumber, bool Success, string? ExternalMessageId, string? ErrorMessage);
public sealed record SendBulkTemplateResponse(int Sent, int Failed, List<BulkTemplateRecipientResult> Results);
