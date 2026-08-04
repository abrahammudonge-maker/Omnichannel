using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class ConversationRepository : IConversationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ConversationRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Conversation?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Conversation>(ConversationQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<Conversation>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Conversation>(ConversationQueries.GetAll, new { OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<Guid> CreateAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(ConversationQueries.Insert, new
        {
            Id = conversation.Id,
            OrganizationId = conversation.OrganizationId,
            CustomerId = conversation.CustomerId,
            Channel = conversation.Channel,
            Status = conversation.Status,
            AssignedUserId = conversation.AssignedUserId,
            CreatedAt = conversation.CreatedAt
        });
        return conversation.Id;
    }

    public async Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(ConversationQueries.Update, conversation);
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(ConversationQueries.Delete, new { Id = id, OrganizationId = organizationId });
    }
}
