using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IChannelAccountRepository
{
    Task<ChannelAccount?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChannelAccount>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>Platform-wide lookup (not scoped to an organization) used to detect a Meta account already connected elsewhere and to route inbound webhooks.</summary>
    Task<ChannelAccount?> FindByExternalAccountIdAsync(string channelType, string externalAccountId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(ChannelAccount channelAccount, CancellationToken cancellationToken);
    Task UpdateAsync(ChannelAccount channelAccount, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
