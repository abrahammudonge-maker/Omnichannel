namespace Omni.Infrastructure.Queries;

public static class OtpQueries
{
    public const string Columns = @"id, organizationid AS OrganizationId, conversationid AS ConversationId, messageid AS MessageId,
        channelaccountid AS ChannelAccountId, phonenumber AS PhoneNumber, purpose AS Purpose, idempotencykey AS IdempotencyKey,
        externalmessageid AS ExternalMessageId, status AS Status, expiresat AS ExpiresAt, createdat AS CreatedAt";

    public const string Insert = @"
        INSERT INTO otp_messages (id, organizationid, conversationid, messageid, channelaccountid, phonenumber, purpose, idempotencykey, externalmessageid, status, expiresat, createdat)
        VALUES (@Id, @OrganizationId, @ConversationId, @MessageId, @ChannelAccountId, @PhoneNumber, @Purpose, @IdempotencyKey, @ExternalMessageId, @Status, @ExpiresAt, @CreatedAt);
    ";

    public static readonly string FindById = $@"
        SELECT {Columns}
        FROM otp_messages
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public static readonly string FindByIdempotencyKey = $@"
        SELECT {Columns}
        FROM otp_messages
        WHERE organizationid = @OrganizationId AND idempotencykey = @IdempotencyKey AND createdat >= @Since;
    ";

    public const string CountSinceForPhone = @"
        SELECT COUNT(1)
        FROM otp_messages
        WHERE organizationid = @OrganizationId AND phonenumber = @PhoneNumber AND createdat >= @Since;
    ";

    public static readonly string GetByMessageIds = $@"
        SELECT {Columns}
        FROM otp_messages
        WHERE organizationid = @OrganizationId AND messageid IN @MessageIds;
    ";

    public const string UpdateStatusByExternalMessageId = @"
        UPDATE otp_messages SET status = @Status
        WHERE organizationid = @OrganizationId AND externalmessageid = @ExternalMessageId;
    ";

    public static readonly string GetRecent = $@"
        SELECT TOP (@Limit) {Columns}
        FROM otp_messages
        WHERE organizationid = @OrganizationId
        ORDER BY createdat DESC;
    ";
}
