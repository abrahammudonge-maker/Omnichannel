using System.Security.Claims;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;

namespace Omni.Api.Authentication;

/// <summary>
/// Works out which WhatsApp number a "whatsapp"/"otp" API key sends from. A key tied to a number always
/// uses that number. An untied key (issued before keys could be tied) falls back to the org's configured
/// OTP number, then to its only active WhatsApp number, so existing keys keep working.
/// </summary>
public sealed class NumberKeyChannelResolver
{
    public const string OtpChannelSettingName = "OtpChannelAccountId";

    private readonly IChannelAccountRepository _channelAccountRepository;
    private readonly IOrganizationSettingRepository _organizationSettingRepository;

    public NumberKeyChannelResolver(IChannelAccountRepository channelAccountRepository, IOrganizationSettingRepository organizationSettingRepository)
    {
        _channelAccountRepository = channelAccountRepository;
        _organizationSettingRepository = organizationSettingRepository;
    }

    public static bool IsNumberKey(ClaimsPrincipal user) =>
        ApiKey.IsNumberScope(user.Claims.FirstOrDefault(c => c.Type == "Scope")?.Value);

    public static Guid? BoundChannelAccountId(ClaimsPrincipal user) =>
        Guid.TryParse(user.Claims.FirstOrDefault(c => c.Type == "ChannelAccountId")?.Value, out var id) ? id : null;

    public async Task<(ChannelAccount? Account, string? Error)> ResolveAsync(ClaimsPrincipal user, Guid organizationId, CancellationToken cancellationToken)
    {
        var accountId = BoundChannelAccountId(user);
        if (accountId is null)
        {
            var settings = await _organizationSettingRepository.GetAllAsync(organizationId, cancellationToken);
            var configured = settings.FirstOrDefault(s => string.Equals(s.SettingName, OtpChannelSettingName, StringComparison.OrdinalIgnoreCase))?.SettingValue;
            if (Guid.TryParse(configured, out var configuredId))
            {
                accountId = configuredId;
            }
        }

        if (accountId is null)
        {
            var active = (await _channelAccountRepository.GetAllAsync(organizationId, cancellationToken)).Where(IsUsable).ToList();
            return active.Count == 1
                ? (active[0], null)
                : (null, "This API key isn't tied to a WhatsApp number. Ask the account admin to tie it to one.");
        }

        var account = await _channelAccountRepository.GetByIdAsync(accountId.Value, organizationId, cancellationToken);
        return account is not null && IsUsable(account)
            ? (account, null)
            : (null, "The WhatsApp number this key sends from isn't connected and active.");
    }

    public static bool IsUsable(ChannelAccount account) =>
        account.ChannelType == "WhatsApp" && account.Status == "Active"
        && !string.IsNullOrWhiteSpace(account.AccessToken) && !string.IsNullOrWhiteSpace(account.ExternalAccountId);
}
