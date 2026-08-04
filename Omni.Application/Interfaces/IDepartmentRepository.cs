using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Department>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(Department department, CancellationToken cancellationToken);
    Task UpdateAsync(Department department, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
