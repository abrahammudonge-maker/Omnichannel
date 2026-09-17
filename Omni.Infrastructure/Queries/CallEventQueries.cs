namespace Omni.Infrastructure.Queries;

public static class CallEventQueries
{
    private const string Columns = "id, organizationid, callid, eventtype, providereventid, payload, createdat";

    public const string Insert = $@"
        INSERT INTO call_events ({Columns})
        OUTPUT INSERTED.id
        VALUES (@Id, @OrganizationId, @CallId, @EventType, @ProviderEventId, @Payload, @CreatedAt);
    ";

    public const string GetByCallId = $@"
        SELECT {Columns}
        FROM call_events
        WHERE callid = @CallId AND organizationid = @OrganizationId
        ORDER BY createdat ASC;
    ";
}
