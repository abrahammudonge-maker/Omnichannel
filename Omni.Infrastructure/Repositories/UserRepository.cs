using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<User?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<User>(UserQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<User>(UserQueries.GetAll, new { OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<User>(UserQueries.GetByEmail, new { Email = email });
    }

    public async Task<Guid> CreateAsync(User user, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(UserQueries.Insert, new
        {
            Id = user.Id,
            OrganizationId = user.OrganizationId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        });
        return user.Id;
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(UserQueries.Update, user);
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(UserQueries.Delete, new { Id = id, OrganizationId = organizationId });
    }
}
