namespace Omni.Infrastructure.Queries;

public static class ApiKeyQueries
{
    public const string Insert = @"
        INSERT INTO api_keys (id, organizationid, name, keyhash, keyprefix, createdat, scope, channelaccountid)
        VALUES (@Id, @OrganizationId, @Name, @KeyHash, @KeyPrefix, @CreatedAt, @Scope, @ChannelAccountId);
    ";

    public const string GetAll = @"
        SELECT id, organizationid, name, keyhash, keyprefix, createdat, lastusedat, revokedat, scope, channelaccountid
        FROM api_keys
        WHERE organizationid = @OrganizationId
        ORDER BY createdat DESC;
    ";

    public const string FindByHash = @"
        SELECT id, organizationid, name, keyhash, keyprefix, createdat, lastusedat, revokedat, scope, channelaccountid
        FROM api_keys
        WHERE keyhash = @KeyHash;
    ";

    public const string Revoke = @"
        UPDATE api_keys SET revokedat = SYSDATETIMEOFFSET()
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string MarkUsed = @"
        UPDATE api_keys SET lastusedat = SYSDATETIMEOFFSET()
        WHERE id = @Id;
    ";

    public const string SetChannelAccount = @"
        UPDATE api_keys SET channelaccountid = @ChannelAccountId
        WHERE id = @Id AND organizationid = @OrganizationId AND revokedat IS NULL AND scope IN ('otp', 'whatsapp');
    ";
}
