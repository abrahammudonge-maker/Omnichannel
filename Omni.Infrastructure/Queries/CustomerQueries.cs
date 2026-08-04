namespace Omni.Infrastructure.Queries;

public static class CustomerQueries
{
    public const string Insert = @"
        INSERT INTO customers (id, organizationid, fullname, phone, email, facebookid, instagramid, whatsappnumber, createdat)
        VALUES (@Id, @OrganizationId, @FullName, @Phone, @Email, @FacebookId, @InstagramId, @WhatsAppNumber, @CreatedAt);
    ";

    public const string GetById = @"
        SELECT id, organizationid, fullname, phone, email, facebookid, instagramid, whatsappnumber, createdat
        FROM customers
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetAll = @"
        SELECT id, organizationid, fullname, phone, email, facebookid, instagramid, whatsappnumber, createdat
        FROM customers
        WHERE organizationid = @OrganizationId
        ORDER BY createdat DESC;
    ";

    public const string Update = @"
        UPDATE customers
        SET fullname = @FullName, phone = @Phone, email = @Email, facebookid = @FacebookId, instagramid = @InstagramId, whatsappnumber = @WhatsAppNumber
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string Delete = @"
        DELETE FROM customers
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";
}
