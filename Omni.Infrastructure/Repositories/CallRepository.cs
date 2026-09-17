using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class CallRepository : ICallRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CallRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Call?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Call>(CallQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<Call>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Call>(CallQueries.GetAll, new { OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<IReadOnlyList<Call>> GetByCustomerIdAsync(Guid customerId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Call>(CallQueries.GetByCustomerId, new { CustomerId = customerId, OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<IReadOnlyList<Call>> GetByConversationIdAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Call>(CallQueries.GetByConversationId, new { ConversationId = conversationId, OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<Call?> FindByProviderCallIdAsync(string provider, string providerCallId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Call>(CallQueries.FindByProviderCallId, new { Provider = provider, ProviderCallId = providerCallId });
    }

    public async Task<int> CountActiveCallsForAgentAsync(Guid agentId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(CallQueries.CountActiveForAgent, new { AgentId = agentId, OrganizationId = organizationId });
    }

    public async Task<DateTimeOffset?> GetLastAssignedAtForAgentAsync(Guid agentId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<DateTimeOffset?>(CallQueries.GetLastAssignedAtForAgent, new { AgentId = agentId, OrganizationId = organizationId });
    }

    public async Task<Guid> CreateAsync(Call call, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<Guid>(CallQueries.Insert, new
        {
            call.Id,
            call.OrganizationId,
            call.CustomerId,
            call.ConversationId,
            call.AgentId,
            call.Provider,
            call.ProviderCallId,
            call.Direction,
            call.FromNumber,
            call.ToNumber,
            call.Status,
            call.StartedAt,
            call.AnsweredAt,
            call.EndedAt,
            call.DurationSeconds,
            call.RecordingUrl,
            call.RecordingStatus,
            call.CreatedAt,
            call.UpdatedAt
        });
    }

    public async Task UpdateAsync(Call call, CancellationToken cancellationToken)
    {
        call.UpdatedAt = DateTimeOffset.UtcNow;
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(CallQueries.Update, new
        {
            call.Id,
            call.OrganizationId,
            call.ConversationId,
            call.AgentId,
            call.ProviderCallId,
            call.Status,
            call.StartedAt,
            call.AnsweredAt,
            call.EndedAt,
            call.DurationSeconds,
            call.RecordingUrl,
            call.RecordingStatus,
            call.UpdatedAt
        });
    }
}
