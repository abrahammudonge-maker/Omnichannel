namespace Omni.Infrastructure.Queries;

public static class OrganizationSettingQueries
{
    public const string Insert = @"
        INSERT INTO organization_settings (id, organizationid, settingname, settingvalue)
        OUTPUT INSERTED.id
        VALUES (@Id, @OrganizationId, @SettingName, @SettingValue);
    ";

    public const string GetById = @"
        SELECT id, organizationid, settingname, settingvalue
        FROM organization_settings
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetAll = @"
        SELECT id, organizationid, settingname, settingvalue
        FROM organization_settings
        WHERE organizationid = @OrganizationId
        ORDER BY settingname;
    ";

    public const string Update = @"
        UPDATE organization_settings
        SET settingname = @SettingName, settingvalue = @SettingValue
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string Delete = @"
        DELETE FROM organization_settings
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetByIdPlatformWide = @"
        SELECT id, organizationid, settingname, settingvalue
        FROM organization_settings
        WHERE id = @Id;
    ";

    public const string GetAllPlatformWide = @"
        SELECT id, organizationid, settingname, settingvalue
        FROM organization_settings
        ORDER BY settingname;
    ";
}
