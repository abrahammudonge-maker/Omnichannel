using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface ICallQueueRepository
{
    Task<CallQueue?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CallQueue>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(CallQueue queue, CancellationToken cancellationToken);
    Task UpdateAsync(CallQueue queue, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}

public interface ICallQueueMemberRepository
{
    Task<IReadOnlyList<CallQueueMember>> GetByQueueIdAsync(Guid queueId, Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(CallQueueMember member, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
