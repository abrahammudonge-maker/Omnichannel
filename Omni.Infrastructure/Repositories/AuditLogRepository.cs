using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuditLogRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<Guid>(AuditLogQueries.Insert, new
        {
            Id = auditLog.Id,
            OrganizationId = auditLog.OrganizationId,
            UserId = auditLog.UserId,
            Action = auditLog.Action,
            Entity = auditLog.Entity,
            EntityId = auditLog.EntityId,
            IpAddress = auditLog.IpAddress,
            Timestamp = auditLog.Timestamp,
            Metadata = auditLog.Metadata
        });

        return id;
    }

    public async Task<IReadOnlyList<AuditLog>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<AuditLog>(AuditLogQueries.GetAll, new { OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<AuditLog?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AuditLog>(AuditLogQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }
}
