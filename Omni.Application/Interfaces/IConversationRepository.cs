using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Conversation>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>Platform-wide lookups (not scoped to an organization) for the platform admin dashboard.</summary>
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Conversation>> GetAllAsync(CancellationToken cancellationToken);
    Task<Guid> CreateAsync(Conversation conversation, CancellationToken cancellationToken);
    Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
