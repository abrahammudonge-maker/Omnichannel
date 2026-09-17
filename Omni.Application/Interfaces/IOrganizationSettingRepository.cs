using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IOrganizationSettingRepository
{
    Task<OrganizationSetting?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrganizationSetting>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>Platform-wide lookups (not scoped to an organization) for the platform admin dashboard.</summary>
    Task<OrganizationSetting?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrganizationSetting>> GetAllAsync(CancellationToken cancellationToken);
    Task<Guid> CreateAsync(OrganizationSetting setting, CancellationToken cancellationToken);
    Task UpdateAsync(OrganizationSetting setting, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
