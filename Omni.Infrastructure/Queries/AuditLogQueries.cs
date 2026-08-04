namespace Omni.Infrastructure.Queries;

public static class AuditLogQueries
{
    public const string Insert = @"
        INSERT INTO audit_logs (id, organizationid, userid, action, entity, entityid, ipaddress, [timestamp], metadata)
        OUTPUT INSERTED.id
        VALUES (@Id, @OrganizationId, @UserId, @Action, @Entity, @EntityId, @IpAddress, @Timestamp, @Metadata);
    ";

    public const string GetById = @"
        SELECT id, organizationid, userid, action, entity, entityid, ipaddress, [timestamp], metadata
        FROM audit_logs
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetAll = @"
        SELECT id, organizationid, userid, action, entity, entityid, ipaddress, [timestamp], metadata
        FROM audit_logs
        WHERE organizationid = @OrganizationId
        ORDER BY [timestamp] DESC;
    ";
}
