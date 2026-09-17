using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface ICallEventRepository
{
    Task<IReadOnlyList<CallEvent>> GetByCallIdAsync(Guid callId, Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(CallEvent callEvent, CancellationToken cancellationToken);
}
