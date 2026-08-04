using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken);
    Task<Guid> CreateAsync(Organization organization, CancellationToken cancellationToken);
    Task UpdateAsync(Organization organization, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
