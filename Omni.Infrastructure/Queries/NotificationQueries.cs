namespace Omni.Infrastructure.Queries;

public static class NotificationQueries
{
    public const string Insert = @"
        INSERT INTO notifications (id, organizationid, userid, conversationid, title, message, isread, createdat)
        OUTPUT INSERTED.id
        VALUES (@Id, @OrganizationId, @UserId, @ConversationId, @Title, @Message, @IsRead, @CreatedAt);
    ";

    public const string GetById = @"
        SELECT id, organizationid, userid, conversationid, title, message, isread, createdat
        FROM notifications
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetByUserId = @"
        SELECT id, organizationid, userid, conversationid, title, message, isread, createdat
        FROM notifications
        WHERE userid = @UserId AND organizationid = @OrganizationId
        ORDER BY createdat DESC;
    ";

    public const string MarkAsRead = @"
        UPDATE notifications
        SET isread = 1
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";
}
