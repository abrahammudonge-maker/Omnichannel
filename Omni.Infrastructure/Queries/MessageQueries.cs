namespace Omni.Infrastructure.Queries;

public static class MessageQueries
{
    public const string Insert = @"
        INSERT INTO messages (id, organizationid, conversationid, direction, messagetype, body, attachmenturl, sentat, status)
        VALUES (@Id, @OrganizationId, @ConversationId, @Direction, @MessageType, @Body, @AttachmentUrl, @SentAt, @Status);
    ";

    public const string GetById = @"
        SELECT id, organizationid, conversationid, direction, messagetype, body, attachmenturl, sentat, status
        FROM messages
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetAll = @"
        SELECT id, organizationid, conversationid, direction, messagetype, body, attachmenturl, sentat, status
        FROM messages
        WHERE organizationid = @OrganizationId
        ORDER BY sentat DESC;
    ";

    public const string GetByConversationId = @"
        SELECT id, organizationid, conversationid, direction, messagetype, body, attachmenturl, sentat, status
        FROM messages
        WHERE conversationid = @ConversationId AND organizationid = @OrganizationId
        ORDER BY sentat ASC;
    ";

    public const string Update = @"
        UPDATE messages
        SET conversationid = @ConversationId, direction = @Direction, messagetype = @MessageType, body = @Body, attachmenturl = @AttachmentUrl, sentat = @SentAt, status = @Status
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string Delete = @"
        DELETE FROM messages
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";
}
