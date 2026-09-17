namespace Omni.Application.Interfaces;

public interface IConversationTagRepository
{
    Task<IReadOnlyList<Guid>> GetTagIdsAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken);
    Task ReplaceAsync(Guid conversationId, Guid organizationId, IReadOnlyCollection<Guid> tagIds, CancellationToken cancellationToken);
}
