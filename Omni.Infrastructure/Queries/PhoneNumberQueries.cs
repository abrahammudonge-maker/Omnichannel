namespace Omni.Infrastructure.Queries;

public static class PhoneNumberQueries
{
    private const string Columns = "id, organizationid, number, provider, providernumberid, displayname, country, status, createdat, updatedat";

    public const string Insert = $@"
        INSERT INTO phone_numbers ({Columns})
        OUTPUT INSERTED.id
        VALUES (@Id, @OrganizationId, @Number, @Provider, @ProviderNumberId, @DisplayName, @Country, @Status, @CreatedAt, @UpdatedAt);
    ";

    public const string GetById = $@"
        SELECT {Columns}
        FROM phone_numbers
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetAll = $@"
        SELECT {Columns}
        FROM phone_numbers
        WHERE organizationid = @OrganizationId
        ORDER BY createdat DESC;
    ";

    public const string FindByNumber = $@"
        SELECT TOP 1 {Columns}
        FROM phone_numbers
        WHERE number = @Number AND status = 'Active';
    ";

    public const string Update = @"
        UPDATE phone_numbers
        SET number = @Number, provider = @Provider, providernumberid = @ProviderNumberId, displayname = @DisplayName,
            country = @Country, status = @Status, updatedat = @UpdatedAt
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string Delete = @"
        DELETE FROM phone_numbers
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";
}
