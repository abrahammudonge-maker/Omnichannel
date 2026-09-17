namespace Omni.Infrastructure.Queries;

public static class DepartmentQueries
{
    public const string Insert = @"
        INSERT INTO departments (id, organizationid, name, description, isactive, createdat, updatedat)
        OUTPUT INSERTED.id
        VALUES (@Id, @OrganizationId, @Name, @Description, @IsActive, @CreatedAt, @UpdatedAt);
    ";

    public const string GetById = @"
        SELECT id, organizationid, name, description, isactive, createdat, updatedat
        FROM departments
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetAll = @"
        SELECT id, organizationid, name, description, isactive, createdat, updatedat
        FROM departments
        WHERE organizationid = @OrganizationId
        ORDER BY createdat DESC;
    ";

    public const string Update = @"
        UPDATE departments
        SET name = @Name, description = @Description, isactive = @IsActive, updatedat = @UpdatedAt
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string Delete = @"
        DELETE FROM departments
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetByIdPlatformWide = @"
        SELECT id, organizationid, name, description, isactive, createdat, updatedat
        FROM departments
        WHERE id = @Id;
    ";

    public const string GetAllPlatformWide = @"
        SELECT id, organizationid, name, description, isactive, createdat, updatedat
        FROM departments
        ORDER BY createdat DESC;
    ";
}
