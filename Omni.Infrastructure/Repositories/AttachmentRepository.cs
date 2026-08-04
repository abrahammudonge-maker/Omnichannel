using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class AttachmentRepository : IAttachmentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AttachmentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(Attachment attachment, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<Guid>(AttachmentQueries.Insert, new
        {
            Id = attachment.Id,
            OrganizationId = attachment.OrganizationId,
            ConversationId = attachment.ConversationId,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            StoragePath = attachment.StoragePath,
            UploadedBy = attachment.UploadedBy,
            UploadedAt = attachment.UploadedAt
        });

        return id;
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(AttachmentQueries.Delete, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<Attachment>> GetByConversationIdAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Attachment>(AttachmentQueries.GetByConversationId, new { ConversationId = conversationId, OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<Attachment?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Attachment>(AttachmentQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }
}
