namespace Omni.Application.DTOs;

/// <summary>Scope is "integrations" (default), "otp" or "whatsapp". ChannelAccountId ties an otp/whatsapp key to its sending number; required for "whatsapp".</summary>
public sealed record CreateApiKeyRequest(string Name, string? Scope = null, Guid? ChannelAccountId = null);

/// <summary>Ties an otp/whatsapp key to a WhatsApp number, or unties it with null.</summary>
public sealed record SetApiKeyChannelRequest(Guid? ChannelAccountId);

/// <summary>The raw key is only ever returned here, at creation time — it isn't retrievable again afterward.</summary>
public sealed record ApiKeyCreatedResponse(Guid Id, string Name, string Key, string KeyPrefix, DateTimeOffset CreatedAt);

public sealed record ApiKeyView(Guid Id, string Name, string KeyPrefix, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt, string Scope, Guid? ChannelAccountId);
