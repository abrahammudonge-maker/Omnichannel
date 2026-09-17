using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Team>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>Platform-wide lookups (not scoped to an organization) for the platform admin dashboard.</summary>
    Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken);
    Task<Guid> CreateAsync(Team team, CancellationToken cancellationToken);
    Task UpdateAsync(Team team, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
