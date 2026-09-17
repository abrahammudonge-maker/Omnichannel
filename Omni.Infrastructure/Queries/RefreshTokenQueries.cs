namespace Omni.Infrastructure.Queries;

public static class RefreshTokenQueries
{
    public const string GetActiveByHash = @"
        SELECT id, userid, tokenhash, expiresat, createdat, revokedat FROM refresh_tokens
        WHERE tokenhash = @TokenHash AND revokedat IS NULL AND expiresat > SYSUTCDATETIME();";
    public const string Insert = @"
        INSERT INTO refresh_tokens (id, userid, tokenhash, expiresat, createdat)
        VALUES (@Id, @UserId, @TokenHash, @ExpiresAt, @CreatedAt);";
    public const string Revoke = @"
        UPDATE refresh_tokens SET revokedat = SYSUTCDATETIME()
        WHERE tokenhash = @TokenHash AND revokedat IS NULL;";
}
