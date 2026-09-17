using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class PhoneNumberRepository : IPhoneNumberRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PhoneNumberRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PhoneNumber?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PhoneNumber>(PhoneNumberQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<PhoneNumber>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<PhoneNumber>(PhoneNumberQueries.GetAll, new { OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<PhoneNumber?> FindByNumberAsync(string number, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PhoneNumber>(PhoneNumberQueries.FindByNumber, new { Number = number });
    }

    public async Task<Guid> CreateAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<Guid>(PhoneNumberQueries.Insert, new
        {
            phoneNumber.Id,
            phoneNumber.OrganizationId,
            phoneNumber.Number,
            phoneNumber.Provider,
            phoneNumber.ProviderNumberId,
            phoneNumber.DisplayName,
            phoneNumber.Country,
            phoneNumber.Status,
            phoneNumber.CreatedAt,
            phoneNumber.UpdatedAt
        });
    }

    public async Task UpdateAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken)
    {
        phoneNumber.UpdatedAt = DateTimeOffset.UtcNow;
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(PhoneNumberQueries.Update, new
        {
            phoneNumber.Id,
            phoneNumber.OrganizationId,
            phoneNumber.Number,
            phoneNumber.Provider,
            phoneNumber.ProviderNumberId,
            phoneNumber.DisplayName,
            phoneNumber.Country,
            phoneNumber.Status,
            phoneNumber.UpdatedAt
        });
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(PhoneNumberQueries.Delete, new { Id = id, OrganizationId = organizationId });
    }
}
