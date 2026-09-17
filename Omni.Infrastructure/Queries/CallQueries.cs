namespace Omni.Infrastructure.Queries;

public static class CallQueries
{
    private const string Columns = "id, organizationid, customerid, conversationid, agentid, provider, providercallid, direction, fromnumber, tonumber, status, startedat, answeredat, endedat, durationseconds, recordingurl, recordingstatus, createdat, updatedat";

    public const string Insert = $@"
        INSERT INTO calls ({Columns})
        OUTPUT INSERTED.id
        VALUES (@Id, @OrganizationId, @CustomerId, @ConversationId, @AgentId, @Provider, @ProviderCallId, @Direction, @FromNumber, @ToNumber, @Status, @StartedAt, @AnsweredAt, @EndedAt, @DurationSeconds, @RecordingUrl, @RecordingStatus, @CreatedAt, @UpdatedAt);
    ";

    public const string GetById = $@"
        SELECT {Columns}
        FROM calls
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetAll = $@"
        SELECT {Columns}
        FROM calls
        WHERE organizationid = @OrganizationId
        ORDER BY createdat DESC;
    ";

    public const string GetByCustomerId = $@"
        SELECT {Columns}
        FROM calls
        WHERE customerid = @CustomerId AND organizationid = @OrganizationId
        ORDER BY createdat DESC;
    ";

    public const string GetByConversationId = $@"
        SELECT {Columns}
        FROM calls
        WHERE conversationid = @ConversationId AND organizationid = @OrganizationId
        ORDER BY createdat ASC;
    ";

    public const string FindByProviderCallId = $@"
        SELECT {Columns}
        FROM calls
        WHERE provider = @Provider AND providercallid = @ProviderCallId;
    ";

    public const string CountActiveForAgent = @"
        SELECT COUNT(1)
        FROM calls
        WHERE agentid = @AgentId AND organizationid = @OrganizationId AND status IN ('Ringing', 'Answered', 'OnHold', 'Transferred');
    ";

    public const string GetLastAssignedAtForAgent = @"
        SELECT MAX(createdat)
        FROM calls
        WHERE agentid = @AgentId AND organizationid = @OrganizationId;
    ";

    public const string Update = @"
        UPDATE calls
        SET conversationid = @ConversationId, agentid = @AgentId, providercallid = @ProviderCallId, status = @Status,
            startedat = @StartedAt, answeredat = @AnsweredAt, endedat = @EndedAt, durationseconds = @DurationSeconds,
            recordingurl = @RecordingUrl, recordingstatus = @RecordingStatus, updatedat = @UpdatedAt
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";
}
