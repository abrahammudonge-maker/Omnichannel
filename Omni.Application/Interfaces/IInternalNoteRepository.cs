using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IInternalNoteRepository
{
    Task<InternalNote?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<InternalNote>> GetByConversationIdAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(InternalNote internalNote, CancellationToken cancellationToken);
    Task UpdateAsync(InternalNote internalNote, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
