using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class TeamRepository : ITeamRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TeamRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(Team team, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<Guid>(TeamQueries.Insert, new
        {
            Id = team.Id,
            OrganizationId = team.OrganizationId,
            DepartmentId = team.DepartmentId,
            Name = team.Name,
            Description = team.Description,
            LeaderId = team.LeaderId,
            IsActive = team.IsActive,
            CreatedAt = team.CreatedAt
        });
        return id;
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(TeamQueries.Delete, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<Team>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Team>(TeamQueries.GetAll, new { OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<Team?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Team>(TeamQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task UpdateAsync(Team team, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(TeamQueries.Update, new
        {
            Id = team.Id,
            OrganizationId = team.OrganizationId,
            DepartmentId = team.DepartmentId,
            Name = team.Name,
            Description = team.Description,
            LeaderId = team.LeaderId,
            IsActive = team.IsActive
        });
    }
}
