using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class CallEventRepository : ICallEventRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CallEventRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CallEvent>> GetByCallIdAsync(Guid callId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<CallEvent>(CallEventQueries.GetByCallId, new { CallId = callId, OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<Guid> CreateAsync(CallEvent callEvent, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<Guid>(CallEventQueries.Insert, new
        {
            callEvent.Id,
            callEvent.OrganizationId,
            callEvent.CallId,
            callEvent.EventType,
            callEvent.ProviderEventId,
            callEvent.Payload,
            callEvent.CreatedAt
        });
    }
}
