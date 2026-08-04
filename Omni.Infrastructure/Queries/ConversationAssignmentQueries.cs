namespace Omni.Infrastructure.Queries;

public static class ConversationAssignmentQueries
{
    public const string Insert = @"
        INSERT INTO conversation_assignments (id, conversationid, assignedto, assignedby, assignedat, reason)
        OUTPUT INSERTED.id
        VALUES (@Id, @ConversationId, @AssignedTo, @AssignedBy, @AssignedAt, @Reason);
    ";

    public const string GetById = @"
        SELECT id, conversationid, assignedto, assignedby, assignedat, reason
        FROM conversation_assignments
        WHERE id = @Id;
    ";

    public const string GetByConversationId = @"
        SELECT id, conversationid, assignedto, assignedby, assignedat, reason
        FROM conversation_assignments
        WHERE conversationid = @ConversationId
        ORDER BY assignedat DESC;
    ";
}
