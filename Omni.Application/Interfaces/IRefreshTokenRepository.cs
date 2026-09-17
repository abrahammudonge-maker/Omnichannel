using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetActiveByHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task CreateAsync(RefreshToken refreshToken, CancellationToken cancellationToken);
    Task<bool> RevokeAsync(string tokenHash, CancellationToken cancellationToken);
}
