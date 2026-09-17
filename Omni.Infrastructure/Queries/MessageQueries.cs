namespace Omni.Infrastructure.Queries;

public static class MessageQueries
{
    public const string Insert = @"
        INSERT INTO messages (id, organizationid, conversationid, externalmessageid, direction, messagetype, body, attachmenturl, sentat, status)
        VALUES (@Id, @OrganizationId, @ConversationId, @ExternalMessageId, @Direction, @MessageType, @Body, @AttachmentUrl, @SentAt, @Status);
    ";

    public const string GetById = @"
        SELECT id, organizationid, conversationid, externalmessageid, direction, messagetype, body, attachmenturl, sentat, status
        FROM messages
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string ExistsByExternalMessageId = @"
        SELECT COUNT(1)
        FROM messages
        WHERE organizationid = @OrganizationId AND externalmessageid = @ExternalMessageId;
    ";

    public const string GetAll = @"
        SELECT id, organizationid, conversationid, externalmessageid, direction, messagetype, body, attachmenturl, sentat, status
        FROM messages
        WHERE organizationid = @OrganizationId
        ORDER BY sentat DESC;
    ";

    public const string GetByConversationId = @"
        SELECT id, organizationid, conversationid, externalmessageid, direction, messagetype, body, attachmenturl, sentat, status
        FROM messages
        WHERE conversationid = @ConversationId AND organizationid = @OrganizationId
        ORDER BY sentat ASC;
    ";

    public const string Update = @"
        UPDATE messages
        SET conversationid = @ConversationId, externalmessageid = @ExternalMessageId, direction = @Direction, messagetype = @MessageType, body = @Body, attachmenturl = @AttachmentUrl, sentat = @SentAt, status = @Status
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    // Meta's status webhooks are delivered at-least-once and can arrive out of order (e.g. a
    // retried "sent" event landing after "read" already did). This only lets status move forward
    // through sent -> delivered -> read, so a late duplicate can't regress an already-newer status.
    public const string UpdateStatusByExternalMessageId = @"
        UPDATE messages
        SET status = @Status
        WHERE organizationid = @OrganizationId AND externalmessageid = @ExternalMessageId
          AND (
                CASE LOWER(status)
                    WHEN 'sent' THEN 1
                    WHEN 'delivered' THEN 2
                    WHEN 'read' THEN 3
                    WHEN 'failed' THEN 4
                    ELSE 0
                END
              ) <= (
                CASE LOWER(@Status)
                    WHEN 'sent' THEN 1
                    WHEN 'delivered' THEN 2
                    WHEN 'read' THEN 3
                    WHEN 'failed' THEN 4
                    ELSE 0
                END
              );
    ";

    public const string Delete = @"
        DELETE FROM messages
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetLastInboundSentAt = @"
        SELECT MAX(sentat)
        FROM messages
        WHERE conversationid = @ConversationId AND organizationid = @OrganizationId AND direction = 'Inbound';
    ";

    public const string MarkOutboundAsRead = @"
        UPDATE messages
        SET status = 'read'
        WHERE conversationid = @ConversationId AND organizationid = @OrganizationId AND direction = 'Outbound' AND status <> 'read';
    ";
}
