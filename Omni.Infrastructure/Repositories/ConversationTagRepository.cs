using Dapper;
using Omni.Application.Interfaces;
using Omni.Infrastructure.Database;

namespace Omni.Infrastructure.Repositories;

public sealed class ConversationTagRepository : IConversationTagRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    public ConversationTagRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<Guid>> GetTagIdsAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT ct.tagid FROM conversation_tags ct INNER JOIN conversations c ON c.id = ct.conversationid WHERE ct.conversationid = @ConversationId AND c.organizationid = @OrganizationId;";
        using var connection = _connectionFactory.CreateConnection();
        return (await connection.QueryAsync<Guid>(sql, new { ConversationId = conversationId, OrganizationId = organizationId })).ToList();
    }

    public async Task ReplaceAsync(Guid conversationId, Guid organizationId, IReadOnlyCollection<Guid> tagIds, CancellationToken cancellationToken)
    {
        const string deleteSql = @"DELETE ct FROM conversation_tags ct INNER JOIN conversations c ON c.id = ct.conversationid WHERE ct.conversationid = @ConversationId AND c.organizationid = @OrganizationId;";
        const string insertSql = @"INSERT INTO conversation_tags (conversationid, tagid) VALUES (@ConversationId, @TagId);";
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync(deleteSql, new { ConversationId = conversationId, OrganizationId = organizationId }, transaction);
        foreach (var tagId in tagIds.Distinct())
            await connection.ExecuteAsync(insertSql, new { ConversationId = conversationId, TagId = tagId }, transaction);
        transaction.Commit();
    }
}
