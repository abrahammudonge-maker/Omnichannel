using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Omni.Application.Interfaces;

namespace Omni.Api.Authentication;

/// <summary>
/// Authenticates server-to-server integration requests via an "X-Api-Key" header instead of the
/// JWT bearer scheme used by logged-in agents. On success, sets an "OrganizationId" claim matching
/// the shape controllers already read for JWT-authenticated requests, plus "ApiKeyId" for rate limiting.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    private const string HeaderName = "X-Api-Key";

    private readonly IApiKeyRepository _apiKeyRepository;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyRepository apiKeyRepository)
        : base(options, logger, encoder)
    {
        _apiKeyRepository = apiKeyRepository;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValue) || string.IsNullOrWhiteSpace(headerValue))
        {
            return AuthenticateResult.Fail($"Missing {HeaderName} header.");
        }

        var apiKey = await _apiKeyRepository.FindByHashAsync(Hash(headerValue.ToString()), Context.RequestAborted);
        if (apiKey is null || apiKey.RevokedAt is not null)
        {
            return AuthenticateResult.Fail("Invalid or revoked API key.");
        }

        await _apiKeyRepository.MarkUsedAsync(apiKey.Id, Context.RequestAborted);

        var claims = new[]
        {
            new Claim("OrganizationId", apiKey.OrganizationId.ToString()),
            new Claim("ApiKeyId", apiKey.Id.ToString())
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
