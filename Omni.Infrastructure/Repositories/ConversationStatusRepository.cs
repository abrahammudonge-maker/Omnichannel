using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class ConversationStatusRepository : IConversationStatusRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ConversationStatusRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(ConversationStatusHistory statusHistory, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<Guid>(ConversationStatusQueries.Insert, new
        {
            Id = statusHistory.Id,
            OrganizationId = statusHistory.OrganizationId,
            ConversationId = statusHistory.ConversationId,
            Status = statusHistory.Status,
            ChangedBy = statusHistory.ChangedBy,
            ChangedAt = statusHistory.ChangedAt,
            Reason = statusHistory.Reason
        });

        return id;
    }

    public async Task<ConversationStatusHistory?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ConversationStatusHistory>(ConversationStatusQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<ConversationStatusHistory>> GetByConversationIdAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<ConversationStatusHistory>(ConversationStatusQueries.GetByConversationId, new { ConversationId = conversationId, OrganizationId = organizationId });
        return result.ToList();
    }
}
