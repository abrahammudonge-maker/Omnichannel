using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IOtpRepository
{
    Task<OtpMessage?> FindByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<OtpMessage?> FindByIdempotencyKeyAsync(Guid organizationId, string idempotencyKey, DateTimeOffset since, CancellationToken cancellationToken);
    Task<int> CountSinceForPhoneAsync(Guid organizationId, string phoneNumber, DateTimeOffset since, CancellationToken cancellationToken);
    Task<IReadOnlyList<OtpMessage>> GetByMessageIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> messageIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<OtpMessage>> GetRecentAsync(Guid organizationId, int limit, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(OtpMessage otp, CancellationToken cancellationToken);
    Task UpdateStatusByExternalMessageIdAsync(string externalMessageId, Guid organizationId, string status, CancellationToken cancellationToken);
}
