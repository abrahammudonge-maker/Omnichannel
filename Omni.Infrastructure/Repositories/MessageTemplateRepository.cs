using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class MessageTemplateRepository : IMessageTemplateRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MessageTemplateRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<MessageTemplate?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<MessageTemplate>(MessageTemplateQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<MessageTemplate>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<MessageTemplate>(MessageTemplateQueries.GetAll, new { OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<IReadOnlyList<MessageTemplate>> GetByChannelAccountIdAsync(Guid channelAccountId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<MessageTemplate>(MessageTemplateQueries.GetByChannelAccountId, new { ChannelAccountId = channelAccountId, OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task UpsertAsync(MessageTemplate template, CancellationToken cancellationToken)
    {
        template.UpdatedAt = DateTimeOffset.UtcNow;
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(MessageTemplateQueries.Upsert, new
        {
            Id = template.Id,
            OrganizationId = template.OrganizationId,
            ChannelAccountId = template.ChannelAccountId,
            Name = template.Name,
            Language = template.Language,
            Category = template.Category,
            Status = template.Status,
            BodyText = template.BodyText,
            ComponentsJson = template.ComponentsJson,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        });
    }
}
