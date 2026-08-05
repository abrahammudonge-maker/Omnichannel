namespace Omni.Infrastructure.Queries;

public static class ChannelAccountQueries
{
    public const string Insert = @"
        INSERT INTO channel_accounts (id, organizationid, channeltype, displayname, externalaccountid, accesstoken, refreshtoken, webhooksecret, smtphost, smtpport, imaphost, imapport, status, createdat, updatedat)
        OUTPUT INSERTED.id
        VALUES (@Id, @OrganizationId, @ChannelType, @DisplayName, @ExternalAccountId, @AccessToken, @RefreshToken, @WebhookSecret, @SmtpHost, @SmtpPort, @ImapHost, @ImapPort, @Status, @CreatedAt, @UpdatedAt);
    ";

    public const string GetById = @"
        SELECT id, organizationid, channeltype, displayname, externalaccountid, accesstoken, refreshtoken, webhooksecret, smtphost, smtpport, imaphost, imapport, status, createdat, updatedat
        FROM channel_accounts
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetAll = @"
        SELECT id, organizationid, channeltype, displayname, externalaccountid, accesstoken, refreshtoken, webhooksecret, smtphost, smtpport, imaphost, imapport, status, createdat, updatedat
        FROM channel_accounts
        WHERE organizationid = @OrganizationId
        ORDER BY createdat DESC;
    ";

    public const string Update = @"
        UPDATE channel_accounts
        SET channeltype = @ChannelType, displayname = @DisplayName, externalaccountid = @ExternalAccountId, accesstoken = @AccessToken, refreshtoken = @RefreshToken, webhooksecret = @WebhookSecret, smtphost = @SmtpHost, smtpport = @SmtpPort, imaphost = @ImapHost, imapport = @ImapPort, status = @Status, updatedat = @UpdatedAt
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string Delete = @"
        DELETE FROM channel_accounts
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";
}
