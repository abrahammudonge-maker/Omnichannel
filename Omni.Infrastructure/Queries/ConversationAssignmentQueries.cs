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
        FROM conversation_assignments ca
        INNER JOIN conversations c ON c.id = ca.conversationid
        WHERE ca.id = @Id AND c.organizationid = @OrganizationId;
    ";

    public const string GetByConversationId = @"
        SELECT id, conversationid, assignedto, assignedby, assignedat, reason
        FROM conversation_assignments ca
        INNER JOIN conversations c ON c.id = ca.conversationid
        WHERE ca.conversationid = @ConversationId AND c.organizationid = @OrganizationId
        ORDER BY ca.assignedat DESC;
    ";
}
