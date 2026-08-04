using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class OrganizationSettingRepository : IOrganizationSettingRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OrganizationSettingRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(OrganizationSetting setting, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<Guid>(OrganizationSettingQueries.Insert, new
        {
            Id = setting.Id,
            OrganizationId = setting.OrganizationId,
            SettingName = setting.SettingName,
            SettingValue = setting.SettingValue
        });

        return id;
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(OrganizationSettingQueries.Delete, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<OrganizationSetting>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<OrganizationSetting>(OrganizationSettingQueries.GetAll, new { OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<OrganizationSetting?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OrganizationSetting>(OrganizationSettingQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task UpdateAsync(OrganizationSetting setting, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(OrganizationSettingQueries.Update, new
        {
            Id = setting.Id,
            OrganizationId = setting.OrganizationId,
            SettingName = setting.SettingName,
            SettingValue = setting.SettingValue
        });
    }
}
