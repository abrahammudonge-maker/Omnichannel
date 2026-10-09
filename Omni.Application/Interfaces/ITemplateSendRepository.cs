using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface ITemplateSendRepository
{
    /// <summary>
    /// Inserts the rows. A row whose idempotency key already exists in the organization isn't inserted;
    /// the existing row is returned in its place with Replayed = true.
    /// </summary>
    Task<IReadOnlyList<(TemplateSend Send, bool Replayed)>> CreateManyAsync(IReadOnlyList<TemplateSend> sends, CancellationToken cancellationToken);

    Task<TemplateSend?> FindByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<TemplateSend?> FindByIdempotencyKeyAsync(Guid organizationId, string idempotencyKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<TemplateSend>> GetByBatchAsync(Guid batchId, Guid organizationId, CancellationToken cancellationToken);

    /// <summary>Atomically moves up to <paramref name="take"/> due rows (any organization) to "sending" and returns them. Safe with several app instances.</summary>
    Task<IReadOnlyList<TemplateSend>> ClaimDueAsync(int take, CancellationToken cancellationToken);

    Task MarkSentAsync(Guid id, Guid conversationId, Guid messageId, string? externalMessageId, CancellationToken cancellationToken);
    Task RescheduleAsync(Guid id, DateTimeOffset nextAttemptAt, string error, Guid? conversationId, Guid? messageId, CancellationToken cancellationToken);
    Task MarkFailedAsync(Guid id, string error, Guid? conversationId, Guid? messageId, CancellationToken cancellationToken);

    /// <summary>Fails rows left in "sending" since before <paramref name="lockedBefore"/> (the app stopped mid-send). Returns how many.</summary>
    Task<int> FailStuckAsync(DateTimeOffset lockedBefore, CancellationToken cancellationToken);

    /// <summary>Applies a Meta delivery status. Only moves forward through sent, delivered, read, failed.</summary>
    Task UpdateStatusByExternalMessageIdAsync(string externalMessageId, Guid organizationId, string status, string? error, CancellationToken cancellationToken);
}

/// <summary>Wakes the template-send dispatcher right after new rows are queued, so sends don't wait for its next poll.</summary>
public interface ITemplateSendSignal
{
    void Notify();
}
