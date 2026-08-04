using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Team>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(Team team, CancellationToken cancellationToken);
    Task UpdateAsync(Team team, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
