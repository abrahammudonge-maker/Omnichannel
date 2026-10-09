using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Queries;

namespace Omni.Infrastructure.Repositories;

public sealed class TemplateSendRepository : ITemplateSendRepository
{
    // SQL Server's duplicate-key errors (unique index / unique constraint).
    private static readonly int[] DuplicateKeyErrors = { 2601, 2627 };

    private readonly IDbConnectionFactory _connectionFactory;

    public TemplateSendRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<(TemplateSend Send, bool Replayed)>> CreateManyAsync(IReadOnlyList<TemplateSend> sends, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        var results = new List<(TemplateSend, bool)>(sends.Count);
        foreach (var send in sends)
        {
            if (send.IdempotencyKey is not null)
            {
                var existing = await connection.QuerySingleOrDefaultAsync<TemplateSend>(TemplateSendQueries.FindByIdempotencyKey, new { send.OrganizationId, send.IdempotencyKey });
                if (existing is not null)
                {
                    results.Add((existing, true));
                    continue;
                }
            }

            try
            {
                await connection.ExecuteAsync(TemplateSendQueries.Insert, send);
                results.Add((send, false));
            }
            catch (SqlException ex) when (DuplicateKeyErrors.Contains(ex.Number) && send.IdempotencyKey is not null)
            {
                // A concurrent request with the same key won the insert.
                var winner = await connection.QuerySingleAsync<TemplateSend>(TemplateSendQueries.FindByIdempotencyKey, new { send.OrganizationId, send.IdempotencyKey });
                results.Add((winner, true));
            }
        }
        return results;
    }

    public async Task<TemplateSend?> FindByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TemplateSend>(TemplateSendQueries.FindById, new { Id = id, OrganizationId = organizationId });
    }

    public async Task<TemplateSend?> FindByIdempotencyKeyAsync(Guid organizationId, string idempotencyKey, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TemplateSend>(TemplateSendQueries.FindByIdempotencyKey, new { OrganizationId = organizationId, IdempotencyKey = idempotencyKey });
    }

    public async Task<IReadOnlyList<TemplateSend>> GetByBatchAsync(Guid batchId, Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<TemplateSend>(TemplateSendQueries.GetByBatch, new { BatchId = batchId, OrganizationId = organizationId });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TemplateSend>> ClaimDueAsync(int take, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<TemplateSend>(TemplateSendQueries.ClaimDue, new { Take = take });
        return rows.ToList();
    }

    public async Task MarkSentAsync(Guid id, Guid conversationId, Guid messageId, string? externalMessageId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(TemplateSendQueries.MarkSent, new { Id = id, ConversationId = conversationId, MessageId = messageId, ExternalMessageId = externalMessageId });
    }

    public async Task RescheduleAsync(Guid id, DateTimeOffset nextAttemptAt, string error, Guid? conversationId, Guid? messageId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(TemplateSendQueries.Reschedule, new { Id = id, NextAttemptAt = nextAttemptAt, Error = Truncate(error), ConversationId = conversationId, MessageId = messageId });
    }

    public async Task MarkFailedAsync(Guid id, string error, Guid? conversationId, Guid? messageId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(TemplateSendQueries.MarkFailed, new { Id = id, Error = Truncate(error), ConversationId = conversationId, MessageId = messageId });
    }

    public async Task<int> FailStuckAsync(DateTimeOffset lockedBefore, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(TemplateSendQueries.FailStuck, new { LockedBefore = lockedBefore });
    }

    public async Task UpdateStatusByExternalMessageIdAsync(string externalMessageId, Guid organizationId, string status, string? error, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(TemplateSendQueries.UpdateStatusByExternalMessageId, new
        {
            OrganizationId = organizationId,
            ExternalMessageId = externalMessageId,
            Status = status.ToLowerInvariant(),
            Error = error is null ? null : Truncate(error)
        });
    }

    private static string Truncate(string value) => value.Length <= 1000 ? value : value[..1000];
}
