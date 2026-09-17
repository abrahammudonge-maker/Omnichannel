using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class ApiKeyRepository : IApiKeyRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ApiKeyRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(ApiKey apiKey, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(ApiKeyQueries.Insert, new
        {
            apiKey.Id,
            apiKey.OrganizationId,
            apiKey.Name,
            apiKey.KeyHash,
            apiKey.KeyPrefix,
            apiKey.CreatedAt
        });
        return apiKey.Id;
    }

    public async Task<IReadOnlyList<ApiKey>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<ApiKey>(ApiKeyQueries.GetAll, new { OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<ApiKey?> FindByHashAsync(string keyHash, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ApiKey>(ApiKeyQueries.FindByHash, new { KeyHash = keyHash });
    }

    public async Task RevokeAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(ApiKeyQueries.Revoke, new { Id = id, OrganizationId = organizationId });
    }

    public async Task MarkUsedAsync(Guid id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(ApiKeyQueries.MarkUsed, new { Id = id });
    }
}
