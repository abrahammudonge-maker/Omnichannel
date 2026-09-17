using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class CallQueueRepository : ICallQueueRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CallQueueRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CallQueue?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CallQueue>(CallQueueQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<CallQueue>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<CallQueue>(CallQueueQueries.GetAll, new { OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<Guid> CreateAsync(CallQueue queue, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<Guid>(CallQueueQueries.Insert, new
        {
            queue.Id,
            queue.OrganizationId,
            queue.DepartmentId,
            queue.Name,
            queue.Description,
            queue.Strategy,
            queue.IsActive,
            queue.CreatedAt
        });
    }

    public async Task UpdateAsync(CallQueue queue, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(CallQueueQueries.Update, new
        {
            queue.Id,
            queue.OrganizationId,
            queue.DepartmentId,
            queue.Name,
            queue.Description,
            queue.Strategy,
            queue.IsActive
        });
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(CallQueueQueries.Delete, new { Id = id, OrganizationId = organizationId });
    }
}

public sealed class CallQueueMemberRepository : ICallQueueMemberRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CallQueueMemberRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CallQueueMember>> GetByQueueIdAsync(Guid queueId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<CallQueueMember>(CallQueueMemberQueries.GetByQueueId, new { QueueId = queueId, OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<Guid> CreateAsync(CallQueueMember member, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<Guid>(CallQueueMemberQueries.Insert, new
        {
            member.Id,
            member.OrganizationId,
            member.QueueId,
            member.UserId,
            member.Priority,
            member.IsActive,
            member.CreatedAt
        });
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(CallQueueMemberQueries.Delete, new { Id = id, OrganizationId = organizationId });
    }
}
