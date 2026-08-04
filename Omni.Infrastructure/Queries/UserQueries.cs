namespace Omni.Infrastructure.Queries;

public static class UserQueries
{
    public const string Insert = @"
        INSERT INTO users (id, organizationid, firstname, lastname, email, passwordhash, role, isactive, createdat)
        VALUES (@Id, @OrganizationId, @FirstName, @LastName, @Email, @PasswordHash, @Role, @IsActive, @CreatedAt);
    ";

    public const string GetById = @"
        SELECT id, organizationid, firstname, lastname, email, passwordhash, role, isactive, createdat
        FROM users
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetAll = @"
        SELECT id, organizationid, firstname, lastname, email, passwordhash, role, isactive, createdat
        FROM users
        WHERE organizationid = @OrganizationId
        ORDER BY createdat DESC;
    ";

    public const string GetByEmail = @"
        SELECT id, organizationid, firstname, lastname, email, passwordhash, role, isactive, createdat
        FROM users
        WHERE email = @Email;
    ";

    public const string Update = @"
        UPDATE users
        SET firstname = @FirstName, lastname = @LastName, email = @Email, passwordhash = @PasswordHash, role = @Role, isactive = @IsActive
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string Delete = @"
        DELETE FROM users
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";
}
