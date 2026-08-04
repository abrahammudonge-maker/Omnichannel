using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IChannelAccountRepository
{
    Task<ChannelAccount?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChannelAccount>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(ChannelAccount channelAccount, CancellationToken cancellationToken);
    Task UpdateAsync(ChannelAccount channelAccount, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
