using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CustomerRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Customer?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Customer>(CustomerQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<Customer>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Customer>(CustomerQueries.GetAll, new { OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Customer>(CustomerQueries.GetByIdPlatformWide, new { Id = id });
    }

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Customer>(CustomerQueries.GetAllPlatformWide);
        return result.ToList();
    }

    public async Task<Guid> CreateAsync(Customer customer, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(CustomerQueries.Insert, new
        {
            Id = customer.Id,
            OrganizationId = customer.OrganizationId,
            FullName = customer.FullName,
            Phone = customer.Phone,
            Email = customer.Email,
            FacebookId = customer.FacebookId,
            InstagramId = customer.InstagramId,
            WhatsAppNumber = customer.WhatsAppNumber,
            CreatedAt = customer.CreatedAt
        });
        return customer.Id;
    }

    public async Task UpdateAsync(Customer customer, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(CustomerQueries.Update, customer);
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(CustomerQueries.Delete, new { Id = id, OrganizationId = organizationId });
    }
}
