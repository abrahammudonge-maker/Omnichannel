using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class ChannelAccountRepository : IChannelAccountRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IDataProtector _protector;

    public ChannelAccountRepository(IDbConnectionFactory connectionFactory, IDataProtectionProvider dataProtectionProvider)
    {
        _connectionFactory = connectionFactory;
        _protector = dataProtectionProvider.CreateProtector("Omnichannel.ChannelAccountSecrets.v1");
    }

    private string? Protect(string? value) => string.IsNullOrEmpty(value) ? value : _protector.Protect(value);

    /// <summary>
    /// Falls back to returning the raw stored value if it isn't protected data — covers rows written
    /// before encryption was added, which are still plaintext until the next time they're saved.
    /// </summary>
    private string? Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        try
        {
            return _protector.Unprotect(value);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return value;
        }
    }

    private ChannelAccount DecryptSecrets(ChannelAccount account)
    {
        account.AccessToken = Unprotect(account.AccessToken);
        account.RefreshToken = Unprotect(account.RefreshToken);
        account.WebhookSecret = Unprotect(account.WebhookSecret);
        return account;
    }

    public async Task<Guid> CreateAsync(ChannelAccount channelAccount, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<Guid>(ChannelAccountQueries.Insert, new
        {
            Id = channelAccount.Id,
            OrganizationId = channelAccount.OrganizationId,
            ChannelType = channelAccount.ChannelType,
            DisplayName = channelAccount.DisplayName,
            ExternalAccountId = channelAccount.ExternalAccountId,
            ExternalWabaId = channelAccount.ExternalWabaId,
            AccessToken = Protect(channelAccount.AccessToken),
            RefreshToken = Protect(channelAccount.RefreshToken),
            WebhookSecret = Protect(channelAccount.WebhookSecret),
            SmtpHost = channelAccount.SmtpHost,
            SmtpPort = channelAccount.SmtpPort,
            ImapHost = channelAccount.ImapHost,
            ImapPort = channelAccount.ImapPort,
            Status = channelAccount.Status,
            CreatedAt = channelAccount.CreatedAt,
            UpdatedAt = channelAccount.UpdatedAt
        });

        return id;
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(ChannelAccountQueries.Delete, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<IReadOnlyList<ChannelAccount>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<ChannelAccount>(ChannelAccountQueries.GetAll, new { OrganizationId = organizationId });
        return result.Select(DecryptSecrets).ToList();
    }

    public async Task<ChannelAccount?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QuerySingleOrDefaultAsync<ChannelAccount>(ChannelAccountQueries.GetById, new { Id = id, OrganizationId = organizationId });
        return result is null ? null : DecryptSecrets(result);
    }

    public async Task<ChannelAccount?> FindByExternalAccountIdAsync(string channelType, string externalAccountId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QuerySingleOrDefaultAsync<ChannelAccount>(ChannelAccountQueries.FindByExternalAccountId, new { ChannelType = channelType, ExternalAccountId = externalAccountId });
        return result is null ? null : DecryptSecrets(result);
    }

    public async Task UpdateAsync(ChannelAccount channelAccount, CancellationToken cancellationToken)
    {
        channelAccount.UpdatedAt = DateTimeOffset.UtcNow;
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(ChannelAccountQueries.Update, new
        {
            Id = channelAccount.Id,
            OrganizationId = channelAccount.OrganizationId,
            ChannelType = channelAccount.ChannelType,
            DisplayName = channelAccount.DisplayName,
            ExternalAccountId = channelAccount.ExternalAccountId,
            ExternalWabaId = channelAccount.ExternalWabaId,
            AccessToken = Protect(channelAccount.AccessToken),
            RefreshToken = Protect(channelAccount.RefreshToken),
            WebhookSecret = Protect(channelAccount.WebhookSecret),
            SmtpHost = channelAccount.SmtpHost,
            SmtpPort = channelAccount.SmtpPort,
            ImapHost = channelAccount.ImapHost,
            ImapPort = channelAccount.ImapPort,
            Status = channelAccount.Status,
            UpdatedAt = channelAccount.UpdatedAt
        });
    }
}
