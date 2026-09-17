using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Message>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Message>> GetByConversationIdAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken);
    Task<DateTimeOffset?> GetLastInboundSentAtAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken);
    Task MarkOutboundAsReadAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken);
    Task<bool> ExistsByExternalMessageIdAsync(string externalMessageId, Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(Message message, CancellationToken cancellationToken);
    Task UpdateAsync(Message message, CancellationToken cancellationToken);
    Task UpdateStatusByExternalMessageIdAsync(string externalMessageId, Guid organizationId, string status, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
