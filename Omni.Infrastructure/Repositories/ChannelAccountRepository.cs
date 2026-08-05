using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class ChannelAccountRepository : IChannelAccountRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ChannelAccountRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
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
            AccessToken = channelAccount.AccessToken,
            RefreshToken = channelAccount.RefreshToken,
            WebhookSecret = channelAccount.WebhookSecret,
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
        return result.ToList();
    }

    public async Task<ChannelAccount?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ChannelAccount>(ChannelAccountQueries.GetById, new { Id = id, OrganizationId = organizationId });
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
            AccessToken = channelAccount.AccessToken,
            RefreshToken = channelAccount.RefreshToken,
            WebhookSecret = channelAccount.WebhookSecret,
            SmtpHost = channelAccount.SmtpHost,
            SmtpPort = channelAccount.SmtpPort,
            ImapHost = channelAccount.ImapHost,
            ImapPort = channelAccount.ImapPort,
            Status = channelAccount.Status,
            UpdatedAt = channelAccount.UpdatedAt
        });
    }
}
