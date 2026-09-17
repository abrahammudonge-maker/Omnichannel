using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IConversationAssignmentRepository
{
    Task<ConversationAssignment?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConversationAssignment>> GetByConversationIdAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(ConversationAssignment assignment, CancellationToken cancellationToken);
}
