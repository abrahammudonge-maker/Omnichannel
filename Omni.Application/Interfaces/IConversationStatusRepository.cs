using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IConversationStatusRepository
{
    Task<ConversationStatusHistory?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConversationStatusHistory>> GetByConversationIdAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(ConversationStatusHistory statusHistory, CancellationToken cancellationToken);
}
