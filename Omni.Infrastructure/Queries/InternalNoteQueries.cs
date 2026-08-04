namespace Omni.Infrastructure.Queries;

public static class InternalNoteQueries
{
    public const string Insert = @"
        INSERT INTO internal_notes (id, organizationid, conversationid, userid, body, createdat, editedat)
        OUTPUT INSERTED.id
        VALUES (@Id, @OrganizationId, @ConversationId, @UserId, @Body, @CreatedAt, @EditedAt);
    ";

    public const string GetById = @"
        SELECT id, organizationid, conversationid, userid, body, createdat, editedat
        FROM internal_notes
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetByConversationId = @"
        SELECT id, organizationid, conversationid, userid, body, createdat, editedat
        FROM internal_notes
        WHERE conversationid = @ConversationId AND organizationid = @OrganizationId
        ORDER BY createdat DESC;
    ";

    public const string Update = @"
        UPDATE internal_notes
        SET body = @Body, editedat = @EditedAt
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string Delete = @"
        DELETE FROM internal_notes
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";
}
