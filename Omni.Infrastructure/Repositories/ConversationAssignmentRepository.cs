using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class ConversationAssignmentRepository : IConversationAssignmentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ConversationAssignmentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(ConversationAssignment assignment, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<Guid>(ConversationAssignmentQueries.Insert, new
        {
            Id = assignment.Id,
            ConversationId = assignment.ConversationId,
            AssignedTo = assignment.AssignedTo,
            AssignedBy = assignment.AssignedBy,
            AssignedAt = assignment.AssignedAt,
            Reason = assignment.Reason
        });

        return id;
    }

    public async Task<ConversationAssignment?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ConversationAssignment>(ConversationAssignmentQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<ConversationAssignment>> GetByConversationIdAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<ConversationAssignment>(ConversationAssignmentQueries.GetByConversationId, new { ConversationId = conversationId, OrganizationId = organizationId });
        return result.ToList();
    }
}
