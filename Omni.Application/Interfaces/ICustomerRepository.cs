using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Customer>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(Customer customer, CancellationToken cancellationToken);
    Task UpdateAsync(Customer customer, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
