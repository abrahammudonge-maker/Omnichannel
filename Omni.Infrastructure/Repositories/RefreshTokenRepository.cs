using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    public RefreshTokenRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;
    public async Task<RefreshToken?> GetActiveByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<RefreshToken>(RefreshTokenQueries.GetActiveByHash, new { TokenHash = tokenHash });
    }
    public async Task CreateAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(RefreshTokenQueries.Insert, refreshToken);
    }
    public async Task<bool> RevokeAsync(string tokenHash, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(RefreshTokenQueries.Revoke, new { TokenHash = tokenHash }) == 1;
    }
}
