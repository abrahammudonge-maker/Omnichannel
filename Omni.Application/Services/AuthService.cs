using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Omni.Application.DTOs;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Domain.Enums;

namespace Omni.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IConfiguration _configuration;

    public AuthService(IUserRepository userRepository, IOrganizationRepository organizationRepository, IRefreshTokenRepository refreshTokenRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _configuration = configuration;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        return await CreateTokenResponseAsync(user, cancellationToken);
    }

    public async Task<LoginResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser is not null)
        {
            throw new InvalidOperationException("User already exists.");
        }

        var organization = new Organization
        {
            Name = request.OrganizationName,
            Email = request.Email,
            Phone = request.Phone,
            Country = request.Country,
            Status = "Active"
        };

        var organizationId = await _organizationRepository.CreateAsync(organization, cancellationToken);

        var user = new User
        {
            OrganizationId = organizationId,
            FirstName = request.AdminFirstName,
            LastName = request.AdminLastName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = Role.OrganizationAdmin,
            IsActive = true
        };

        await _userRepository.CreateAsync(user, cancellationToken);
        return await CreateTokenResponseAsync(user, cancellationToken);
    }

    public async Task<LoginResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is required.");
        }

        var tokenHash = HashToken(refreshToken);
        var token = await _refreshTokenRepository.GetActiveByHashAsync(tokenHash, cancellationToken);
        if (token is null)
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
        var user = await _userRepository.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("User account is no longer active.");
        if (!await _refreshTokenRepository.RevokeAsync(tokenHash, cancellationToken))
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
        return await CreateTokenResponseAsync(user, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is required.");
        }

        await _refreshTokenRepository.RevokeAsync(HashToken(refreshToken), cancellationToken);
    }

    private async Task<LoginResponse> CreateTokenResponseAsync(User user, CancellationToken cancellationToken)
    {
        var issuer = _configuration["Jwt:Issuer"] ?? "https://localhost";
        var audience = _configuration["Jwt:Audience"] ?? "omnichannel";
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT signing key is not configured."));
        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var parsedMinutes) ? parsedMinutes : 60;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("UserId", user.Id.ToString()),
            new("OrganizationId", user.OrganizationId.ToString()),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        await _refreshTokenRepository.CreateAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        }, cancellationToken);
        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), refreshToken, user.Id, user.OrganizationId, user.Role.ToString());
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
