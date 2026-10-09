using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IApiKeyRepository
{
    Task<Guid> CreateAsync(ApiKey apiKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApiKey>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>Platform-wide lookup by hash (not scoped to an organization) — used by API-key authentication to find which organization a key belongs to.</summary>
    Task<ApiKey?> FindByHashAsync(string keyHash, CancellationToken cancellationToken);
    Task RevokeAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task MarkUsedAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Ties an active key to a WhatsApp number (or unties it with null). Returns false if the key isn't found or is revoked.</summary>
    Task<bool> SetChannelAccountAsync(Guid id, Guid organizationId, Guid? channelAccountId, CancellationToken cancellationToken);
}
