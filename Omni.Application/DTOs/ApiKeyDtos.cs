namespace Omni.Application.DTOs;

public sealed record CreateApiKeyRequest(string Name);

/// <summary>The raw key is only ever returned here, at creation time — it isn't retrievable again afterward.</summary>
public sealed record ApiKeyCreatedResponse(Guid Id, string Name, string Key, string KeyPrefix, DateTimeOffset CreatedAt);

public sealed record ApiKeyView(Guid Id, string Name, string KeyPrefix, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt);
