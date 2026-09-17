using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class MessageRepository : IMessageRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MessageRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Message?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Message>(MessageQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<Message>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Message>(MessageQueries.GetAll, new { OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<IReadOnlyList<Message>> GetByConversationIdAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Message>(MessageQueries.GetByConversationId, new { ConversationId = conversationId, OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<DateTimeOffset?> GetLastInboundSentAtAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<DateTimeOffset?>(MessageQueries.GetLastInboundSentAt, new { ConversationId = conversationId, OrganizationId = organizationId });
    }

    public async Task MarkOutboundAsReadAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(MessageQueries.MarkOutboundAsRead, new { ConversationId = conversationId, OrganizationId = organizationId });
    }

    public async Task<bool> ExistsByExternalMessageIdAsync(string externalMessageId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(MessageQueries.ExistsByExternalMessageId, new { ExternalMessageId = externalMessageId, OrganizationId = organizationId }) > 0;
    }

    public async Task<Guid> CreateAsync(Message message, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(MessageQueries.Insert, new
        {
            Id = message.Id,
            OrganizationId = message.OrganizationId,
            ConversationId = message.ConversationId,
            ExternalMessageId = message.ExternalMessageId,
            Direction = message.Direction,
            MessageType = message.MessageType,
            Body = message.Body,
            AttachmentUrl = message.AttachmentUrl,
            SentAt = message.SentAt,
            Status = message.Status
        });
        return message.Id;
    }

    public async Task UpdateAsync(Message message, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(MessageQueries.Update, message);
    }

    public async Task UpdateStatusByExternalMessageIdAsync(string externalMessageId, Guid organizationId, string status, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(MessageQueries.UpdateStatusByExternalMessageId, new
        {
            ExternalMessageId = externalMessageId,
            OrganizationId = organizationId,
            Status = status
        });
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(MessageQueries.Delete, new { Id = id, OrganizationId = organizationId });
    }
}
