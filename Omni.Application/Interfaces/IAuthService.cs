using Omni.Application.DTOs;

namespace Omni.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<LoginResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<LoginResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
}
