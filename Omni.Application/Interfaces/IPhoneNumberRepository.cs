using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IPhoneNumberRepository
{
    Task<PhoneNumber?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PhoneNumber>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>Platform-wide lookup by the dialed/caller number — used to identify which organization an inbound call webhook belongs to.</summary>
    Task<PhoneNumber?> FindByNumberAsync(string number, CancellationToken cancellationToken);

    Task<Guid> CreateAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken);
    Task UpdateAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
}
