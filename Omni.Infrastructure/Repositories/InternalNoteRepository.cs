using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class InternalNoteRepository : IInternalNoteRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public InternalNoteRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(InternalNote internalNote, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<Guid>(InternalNoteQueries.Insert, new
        {
            Id = internalNote.Id,
            OrganizationId = internalNote.OrganizationId,
            ConversationId = internalNote.ConversationId,
            UserId = internalNote.UserId,
            Body = internalNote.Body,
            CreatedAt = internalNote.CreatedAt,
            EditedAt = internalNote.EditedAt
        });

        return id;
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(InternalNoteQueries.Delete, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<InternalNote>> GetByConversationIdAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<InternalNote>(InternalNoteQueries.GetByConversationId, new { ConversationId = conversationId, OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<InternalNote?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<InternalNote>(InternalNoteQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task UpdateAsync(InternalNote internalNote, CancellationToken cancellationToken)
    {
        internalNote.EditedAt = DateTimeOffset.UtcNow;
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(InternalNoteQueries.Update, new
        {
            Id = internalNote.Id,
            OrganizationId = internalNote.OrganizationId,
            Body = internalNote.Body,
            EditedAt = internalNote.EditedAt
        });
    }
}
