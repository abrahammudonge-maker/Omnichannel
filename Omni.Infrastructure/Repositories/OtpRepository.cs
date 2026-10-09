using Dapper;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class OtpRepository : IOtpRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OtpRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<OtpMessage?> FindByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OtpMessage>(OtpQueries.FindById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<OtpMessage?> FindByIdempotencyKeyAsync(Guid organizationId, string idempotencyKey, DateTimeOffset since, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OtpMessage>(OtpQueries.FindByIdempotencyKey, new { OrganizationId = organizationId, IdempotencyKey = idempotencyKey, Since = since });
    }

    public async Task<int> CountSinceForPhoneAsync(Guid organizationId, string phoneNumber, DateTimeOffset since, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(OtpQueries.CountSinceForPhone, new { OrganizationId = organizationId, PhoneNumber = phoneNumber, Since = since });
    }

    public async Task<IReadOnlyList<OtpMessage>> GetByMessageIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> messageIds, CancellationToken cancellationToken)
    {
        if (messageIds.Count == 0) return Array.Empty<OtpMessage>();
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<OtpMessage>(OtpQueries.GetByMessageIds, new { OrganizationId = organizationId, MessageIds = messageIds.ToArray() });
        return result.ToList();
    }

    public async Task<IReadOnlyList<OtpMessage>> GetRecentAsync(Guid organizationId, int limit, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<OtpMessage>(OtpQueries.GetRecent, new { OrganizationId = organizationId, Limit = limit });
        return result.ToList();
    }

    public async Task<Guid> CreateAsync(OtpMessage otp, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(OtpQueries.Insert, new
        {
            otp.Id,
            otp.OrganizationId,
            otp.ConversationId,
            otp.MessageId,
            otp.ChannelAccountId,
            otp.PhoneNumber,
            otp.Purpose,
            otp.IdempotencyKey,
            otp.ExternalMessageId,
            otp.Status,
            otp.ExpiresAt,
            otp.CreatedAt
        });
        return otp.Id;
    }

    public async Task UpdateStatusByExternalMessageIdAsync(string externalMessageId, Guid organizationId, string status, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(OtpQueries.UpdateStatusByExternalMessageId, new { OrganizationId = organizationId, ExternalMessageId = externalMessageId, Status = status });
    }
}
