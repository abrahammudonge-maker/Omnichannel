namespace Omni.Infrastructure.Queries;

public static class CallQueueQueries
{
    private const string Columns = "id, organizationid, departmentid, name, description, strategy, isactive, createdat";

    public const string Insert = $@"
        INSERT INTO call_queues ({Columns})
        OUTPUT INSERTED.id
        VALUES (@Id, @OrganizationId, @DepartmentId, @Name, @Description, @Strategy, @IsActive, @CreatedAt);
    ";

    public const string GetById = $@"
        SELECT {Columns}
        FROM call_queues
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetAll = $@"
        SELECT {Columns}
        FROM call_queues
        WHERE organizationid = @OrganizationId
        ORDER BY name ASC;
    ";

    public const string Update = @"
        UPDATE call_queues
        SET departmentid = @DepartmentId, name = @Name, description = @Description, strategy = @Strategy, isactive = @IsActive
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string Delete = @"
        DELETE FROM call_queues
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";
}

public static class CallQueueMemberQueries
{
    private const string Columns = "id, organizationid, queueid, userid, priority, isactive, createdat";

    public const string Insert = $@"
        INSERT INTO call_queue_members ({Columns})
        OUTPUT INSERTED.id
        VALUES (@Id, @OrganizationId, @QueueId, @UserId, @Priority, @IsActive, @CreatedAt);
    ";

    public const string GetByQueueId = $@"
        SELECT {Columns}
        FROM call_queue_members
        WHERE queueid = @QueueId AND organizationid = @OrganizationId
        ORDER BY priority ASC;
    ";

    public const string Delete = @"
        DELETE FROM call_queue_members
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";
}
