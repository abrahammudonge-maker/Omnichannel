namespace Omni.Infrastructure.Queries;

public static class TemplateSendQueries
{
    public const string Columns = @"id, organizationid, apikeyid, batchid, channelaccountid, templateid, phonenumber, customername,
        bodyparametersjson, idempotencykey, status, attempts, nextattemptat, lockedat, conversationid, messageid,
        externalmessageid, error, createdat, updatedat";

    public const string Insert = @"
        INSERT INTO template_sends (id, organizationid, apikeyid, batchid, channelaccountid, templateid, phonenumber, customername,
            bodyparametersjson, idempotencykey, status, attempts, nextattemptat, createdat, updatedat)
        VALUES (@Id, @OrganizationId, @ApiKeyId, @BatchId, @ChannelAccountId, @TemplateId, @PhoneNumber, @CustomerName,
            @BodyParametersJson, @IdempotencyKey, @Status, 0, @NextAttemptAt, @CreatedAt, @UpdatedAt);
    ";

    public static readonly string FindById = $@"
        SELECT {Columns} FROM template_sends
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public static readonly string FindByIdempotencyKey = $@"
        SELECT {Columns} FROM template_sends
        WHERE organizationid = @OrganizationId AND idempotencykey = @IdempotencyKey;
    ";

    public static readonly string GetByBatch = $@"
        SELECT {Columns} FROM template_sends
        WHERE organizationid = @OrganizationId AND batchid = @BatchId
        ORDER BY createdat;
    ";

    // READPAST skips rows another instance has locked, so two dispatchers never claim the same row.
    public static readonly string ClaimDue = $@"
        WITH due AS (
            SELECT TOP (@Take) *
            FROM template_sends WITH (ROWLOCK, UPDLOCK, READPAST)
            WHERE status = 'queued' AND nextattemptat <= SYSDATETIMEOFFSET()
            ORDER BY nextattemptat
        )
        UPDATE due
        SET status = 'sending', lockedat = SYSDATETIMEOFFSET(), attempts = attempts + 1, updatedat = SYSDATETIMEOFFSET()
        OUTPUT {Prefixed("inserted")};
    ";

    public const string MarkSent = @"
        UPDATE template_sends
        SET status = 'sent', conversationid = @ConversationId, messageid = @MessageId, externalmessageid = @ExternalMessageId,
            error = NULL, lockedat = NULL, updatedat = SYSDATETIMEOFFSET()
        WHERE id = @Id AND status = 'sending';
    ";

    public const string Reschedule = @"
        UPDATE template_sends
        SET status = 'queued', nextattemptat = @NextAttemptAt, error = @Error, lockedat = NULL,
            conversationid = COALESCE(@ConversationId, conversationid), messageid = COALESCE(@MessageId, messageid), updatedat = SYSDATETIMEOFFSET()
        WHERE id = @Id;
    ";

    public const string MarkFailed = @"
        UPDATE template_sends
        SET status = 'failed', error = @Error, lockedat = NULL,
            conversationid = COALESCE(@ConversationId, conversationid), messageid = COALESCE(@MessageId, messageid), updatedat = SYSDATETIMEOFFSET()
        WHERE id = @Id;
    ";

    public const string FailStuck = @"
        UPDATE template_sends
        SET status = 'failed', lockedat = NULL, updatedat = SYSDATETIMEOFFSET(),
            error = 'The service restarted while this message was being sent, so delivery is unknown. Check with the customer before resending.'
        WHERE status = 'sending' AND lockedat < @LockedBefore;
    ";

    // Same forward-only rule as messages: Meta's status webhooks are at-least-once and can arrive out of order.
    public const string UpdateStatusByExternalMessageId = @"
        UPDATE template_sends
        SET status = @Status, error = CASE WHEN @Status = 'failed' THEN COALESCE(@Error, error) ELSE error END, updatedat = SYSDATETIMEOFFSET()
        WHERE organizationid = @OrganizationId AND externalmessageid = @ExternalMessageId
          AND (CASE status WHEN 'sent' THEN 1 WHEN 'delivered' THEN 2 WHEN 'read' THEN 3 WHEN 'failed' THEN 4 ELSE 5 END)
            < (CASE @Status WHEN 'sent' THEN 1 WHEN 'delivered' THEN 2 WHEN 'read' THEN 3 WHEN 'failed' THEN 4 ELSE 0 END);
    ";

    private static string Prefixed(string alias) =>
        string.Join(", ", Columns.Split(',').Select(c => $"{alias}.{c.Trim()}"));
}
