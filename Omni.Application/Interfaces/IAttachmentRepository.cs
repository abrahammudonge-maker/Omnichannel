using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IAttachmentRepository
{
    Task<Attachment?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Attachment>> GetByConversationIdAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(Attachment attachment, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
