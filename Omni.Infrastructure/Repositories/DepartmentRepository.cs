using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DepartmentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(Department department, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<Guid>(DepartmentQueries.Insert, new
        {
            Id = department.Id,
            OrganizationId = department.OrganizationId,
            Name = department.Name,
            Description = department.Description,
            IsActive = department.IsActive,
            CreatedAt = department.CreatedAt,
            UpdatedAt = department.UpdatedAt
        });
        return id;
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(DepartmentQueries.Delete, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<Department>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Department>(DepartmentQueries.GetAll, new { OrganizationId = organizationId });
        return result.ToList();
    }

    public async Task<Department?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Department>(DepartmentQueries.GetById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Department>(DepartmentQueries.GetByIdPlatformWide, new { Id = id });
    }

    public async Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Department>(DepartmentQueries.GetAllPlatformWide);
        return result.ToList();
    }

    public async Task UpdateAsync(Department department, CancellationToken cancellationToken)
    {
        department.UpdatedAt = DateTimeOffset.UtcNow;
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(DepartmentQueries.Update, new
        {
            Id = department.Id,
            OrganizationId = department.OrganizationId,
            Name = department.Name,
            Description = department.Description,
            IsActive = department.IsActive,
            UpdatedAt = department.UpdatedAt
        });
    }
}
