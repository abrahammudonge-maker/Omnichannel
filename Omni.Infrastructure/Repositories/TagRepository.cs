using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class TagRepository : ITagRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TagRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(Tag tag, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<Guid>(TagQueries.Insert, new
        {
            Id = tag.Id,
            OrganizationId = tag.OrganizationId,
            Name = tag.Name,
            Description = tag.Description,
            IsActive = tag.IsActive,
            CreatedAt = tag.CreatedAt
        });

        return id;
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(TagQueries.Delete, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<Tag>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Tag>(TagQueries.GetAll, new { OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<Tag?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Tag>(TagQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task UpdateAsync(Tag tag, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(TagQueries.Update, new
        {
            Id = tag.Id,
            OrganizationId = tag.OrganizationId,
            Name = tag.Name,
            Description = tag.Description,
            IsActive = tag.IsActive
        });
    }
}
