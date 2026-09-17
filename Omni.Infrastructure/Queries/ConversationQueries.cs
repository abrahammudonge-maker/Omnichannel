namespace Omni.Infrastructure.Queries;

public static class ConversationQueries
{
    public const string Insert = @"
        INSERT INTO conversations (id, organizationid, customerid, channelaccountid, channel, status, assigneduserid, createdat)
        VALUES (@Id, @OrganizationId, @CustomerId, @ChannelAccountId, @Channel, @Status, @AssignedUserId, @CreatedAt);
    ";

    public const string GetById = @"
        SELECT id, organizationid, customerid, channelaccountid, channel, status, assigneduserid, createdat
        FROM conversations
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetAll = @"
        SELECT id, organizationid, customerid, channelaccountid, channel, status, assigneduserid, createdat
        FROM conversations
        WHERE organizationid = @OrganizationId
        ORDER BY createdat DESC;
    ";

    public const string Update = @"
        UPDATE conversations
        SET customerid = @CustomerId, channelaccountid = @ChannelAccountId, channel = @Channel, status = @Status, assigneduserid = @AssignedUserId
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string Delete = @"
        DELETE FROM conversations
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetByIdPlatformWide = @"
        SELECT id, organizationid, customerid, channelaccountid, channel, status, assigneduserid, createdat
        FROM conversations
        WHERE id = @Id;
    ";

    public const string GetAllPlatformWide = @"
        SELECT id, organizationid, customerid, channelaccountid, channel, status, assigneduserid, createdat
        FROM conversations
        ORDER BY createdat DESC;
    ";
}
