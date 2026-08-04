using System.Data;
using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OrganizationRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Organization>(OrganizationQueries.GetById, new { Id = id });
    }

    public async Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Organization>(OrganizationQueries.GetAll);
        return result.ToList();
    }

    public async Task<Guid> CreateAsync(Organization organization, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<Guid>(OrganizationQueries.Insert, new
        {
            Id = organization.Id,
            Name = organization.Name,
            Email = organization.Email,
            Phone = organization.Phone,
            Country = organization.Country,
            Status = organization.Status,
            CreatedAt = organization.CreatedAt
        });
        return id;
    }

    public async Task UpdateAsync(Organization organization, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(OrganizationQueries.Update, organization);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(OrganizationQueries.Delete, new { Id = id });
    }
}
