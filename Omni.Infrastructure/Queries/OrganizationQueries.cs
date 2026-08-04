namespace Omni.Infrastructure.Queries;

public static class OrganizationQueries
{
    public const string Insert = @"
        INSERT INTO organizations (id, name, email, phone, country, status, createdat)
        OUTPUT INSERTED.id
        VALUES (@Id, @Name, @Email, @Phone, @Country, @Status, @CreatedAt);
    ";

    public const string GetById = @"
        SELECT id, name, email, phone, country, status, createdat
        FROM organizations
        WHERE id = @Id;
    ";

    public const string GetAll = @"
        SELECT id, name, email, phone, country, status, createdat
        FROM organizations
        ORDER BY createdat DESC;
    ";

    public const string Update = @"
        UPDATE organizations
        SET name = @Name, email = @Email, phone = @Phone, country = @Country, status = @Status
        WHERE id = @Id;
    ";

    public const string Delete = @"
        DELETE FROM organizations
        WHERE id = @Id;
    ";
}
