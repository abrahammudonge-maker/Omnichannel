namespace Omni.Infrastructure.Queries;

public static class ConversationStatusQueries
{
    public const string Insert = @"
        INSERT INTO conversation_status_history (id, organizationid, conversationid, status, changedby, changedat, reason)
        OUTPUT INSERTED.id
        VALUES (@Id, @OrganizationId, @ConversationId, @Status, @ChangedBy, @ChangedAt, @Reason);
    ";

    public const string GetById = @"
        SELECT id, organizationid, conversationid, status, changedby, changedat, reason
        FROM conversation_status_history
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetByConversationId = @"
        SELECT id, organizationid, conversationid, status, changedby, changedat, reason
        FROM conversation_status_history
        WHERE conversationid = @ConversationId AND organizationid = @OrganizationId
        ORDER BY changedat DESC;
    ";
}
