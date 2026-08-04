using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IConversationAssignmentRepository
{
    Task<ConversationAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConversationAssignment>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(ConversationAssignment assignment, CancellationToken cancellationToken);
}
