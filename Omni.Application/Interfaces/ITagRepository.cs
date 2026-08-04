using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface ITagRepository
{
    Task<Tag?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Tag>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(Tag tag, CancellationToken cancellationToken);
    Task UpdateAsync(Tag tag, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
