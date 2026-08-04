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
    private readonly IConfiguration _configuration;

    public AuthService(IUserRepository userRepository, IOrganizationRepository organizationRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _configuration = configuration;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        return CreateTokenResponse(user);
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
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            Country = request.Country,
            Status = "Active"
        };

        var organizationId = await _organizationRepository.CreateAsync(organization, cancellationToken);

        var user = new User
        {
            OrganizationId = organizationId,
            FirstName = request.Name,
            LastName = "Admin",
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = Role.OrganizationAdmin,
            IsActive = true
        };

        await _userRepository.CreateAsync(user, cancellationToken);
        return CreateTokenResponse(user);
    }

    public Task<LoginResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is required.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Email = "demo@example.com",
            Role = Role.Agent,
            PasswordHash = string.Empty
        };

        return Task.FromResult(CreateTokenResponse(user));
    }

    public Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is required.");
        }

        return Task.CompletedTask;
    }

    private LoginResponse CreateTokenResponse(User user)
    {
        var issuer = _configuration["Jwt:Issuer"] ?? "https://localhost";
        var audience = _configuration["Jwt:Audience"] ?? "omnichannel";
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "super-secret-key-for-development-1234567890");

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
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), refreshToken, user.Id, user.OrganizationId, user.Role.ToString());
    }
}
