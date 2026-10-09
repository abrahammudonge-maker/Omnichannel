namespace Omni.Application.DTOs;

public sealed record SendOtpRequest(string PhoneNumber, string Code, int ExpiresInMinutes, string? Purpose, Guid? ChannelAccountId, string? IdempotencyKey);

public sealed record SendOtpResponse(Guid OtpId, bool Accepted, string Status, Guid? ConversationId, DateTimeOffset ExpiresAt, bool Replayed, string? Error);

public sealed record OtpStatusView(Guid OtpId, string PhoneNumber, string? Purpose, string Status, Guid? ConversationId, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
