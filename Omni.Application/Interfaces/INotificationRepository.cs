using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Notification>> GetByUserIdAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(Notification notification, CancellationToken cancellationToken);
    Task MarkAsReadAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
