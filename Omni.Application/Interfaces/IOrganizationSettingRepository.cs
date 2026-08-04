using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IOrganizationSettingRepository
{
    Task<OrganizationSetting?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrganizationSetting>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(OrganizationSetting setting, CancellationToken cancellationToken);
    Task UpdateAsync(OrganizationSetting setting, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
