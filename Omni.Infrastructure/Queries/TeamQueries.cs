namespace Omni.Infrastructure.Queries;

public static class TeamQueries
{
    public const string Insert = @"
        INSERT INTO teams (id, organizationid, departmentid, name, description, leaderid, isactive, createdat)
        OUTPUT INSERTED.id
        VALUES (@Id, @OrganizationId, @DepartmentId, @Name, @Description, @LeaderId, @IsActive, @CreatedAt);
    ";

    public const string GetById = @"
        SELECT id, organizationid, departmentid, name, description, leaderid, isactive, createdat
        FROM teams
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetAll = @"
        SELECT id, organizationid, departmentid, name, description, leaderid, isactive, createdat
        FROM teams
        WHERE organizationid = @OrganizationId
        ORDER BY createdat DESC;
    ";

    public const string Update = @"
        UPDATE teams
        SET departmentid = @DepartmentId, name = @Name, description = @Description, leaderid = @LeaderId, isactive = @IsActive
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string Delete = @"
        DELETE FROM teams
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";
}
