using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IMessageTemplateRepository
{
    Task<MessageTemplate?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MessageTemplate>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MessageTemplate>> GetByChannelAccountIdAsync(Guid channelAccountId, Guid organizationId, CancellationToken cancellationToken);

    /// <summary>Inserts a newly-seen template, or updates one already synced by (channelaccountid, name, language).</summary>
    Task UpsertAsync(MessageTemplate template, CancellationToken cancellationToken);
}
