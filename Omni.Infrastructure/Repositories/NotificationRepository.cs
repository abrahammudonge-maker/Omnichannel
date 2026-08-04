using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public NotificationRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(Notification notification, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<Guid>(NotificationQueries.Insert, new
        {
            Id = notification.Id,
            OrganizationId = notification.OrganizationId,
            UserId = notification.UserId,
            Title = notification.Title,
            Message = notification.Message,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt
        });

        return id;
    }

    public async Task<Notification?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Notification>(NotificationQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<Notification>> GetByUserIdAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Notification>(NotificationQueries.GetByUserId, new { UserId = userId, OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task MarkAsReadAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(NotificationQueries.MarkAsRead, new { Id = id, OrganizationId = organizationId });
    }
}
