using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IAuditLogRepository
{
    Task<AuditLog?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditLog>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(AuditLog auditLog, CancellationToken cancellationToken);
}
