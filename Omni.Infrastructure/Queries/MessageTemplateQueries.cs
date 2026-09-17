namespace Omni.Infrastructure.Queries;

public static class MessageTemplateQueries
{
    public const string GetById = @"
        SELECT id, organizationid, channelaccountid, name, language, category, status, bodytext, componentsjson, createdat, updatedat
        FROM message_templates
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetAll = @"
        SELECT id, organizationid, channelaccountid, name, language, category, status, bodytext, componentsjson, createdat, updatedat
        FROM message_templates
        WHERE organizationid = @OrganizationId
        ORDER BY name ASC;
    ";

    public const string GetByChannelAccountId = @"
        SELECT id, organizationid, channelaccountid, name, language, category, status, bodytext, componentsjson, createdat, updatedat
        FROM message_templates
        WHERE channelaccountid = @ChannelAccountId AND organizationid = @OrganizationId
        ORDER BY name ASC;
    ";

    // Keyed by (channelaccountid, name, language) to match Meta's own template uniqueness rule per WABA.
    public const string Upsert = @"
        MERGE message_templates AS target
        USING (SELECT @ChannelAccountId AS channelaccountid, @Name AS name, @Language AS language) AS source
        ON target.channelaccountid = source.channelaccountid AND target.name = source.name AND target.language = source.language
        WHEN MATCHED THEN
            UPDATE SET category = @Category, status = @Status, bodytext = @BodyText, componentsjson = @ComponentsJson, updatedat = @UpdatedAt
        WHEN NOT MATCHED THEN
            INSERT (id, organizationid, channelaccountid, name, language, category, status, bodytext, componentsjson, createdat, updatedat)
            VALUES (@Id, @OrganizationId, @ChannelAccountId, @Name, @Language, @Category, @Status, @BodyText, @ComponentsJson, @CreatedAt, @UpdatedAt);
    ";
}
