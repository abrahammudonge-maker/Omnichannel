namespace Omni.Infrastructure.Queries;

public static class TagQueries
{
    public const string Insert = @"
        INSERT INTO tags (id, organizationid, name, description, isactive, createdat)
        OUTPUT INSERTED.id
        VALUES (@Id, @OrganizationId, @Name, @Description, @IsActive, @CreatedAt);
    ";

    public const string GetById = @"
        SELECT id, organizationid, name, description, isactive, createdat
        FROM tags
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetAll = @"
        SELECT id, organizationid, name, description, isactive, createdat
        FROM tags
        WHERE organizationid = @OrganizationId
        ORDER BY createdat DESC;
    ";

    public const string Update = @"
        UPDATE tags
        SET name = @Name, description = @Description, isactive = @IsActive
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string Delete = @"
        DELETE FROM tags
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";
}
