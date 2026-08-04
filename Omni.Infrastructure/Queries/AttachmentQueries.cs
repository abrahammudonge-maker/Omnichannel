namespace Omni.Infrastructure.Queries;

public static class AttachmentQueries
{
    public const string Insert = @"
        INSERT INTO attachments (id, organizationid, conversationid, filename, contenttype, filesize, storagepath, uploadedby, uploadedat)
        OUTPUT INSERTED.id
        VALUES (@Id, @OrganizationId, @ConversationId, @FileName, @ContentType, @FileSize, @StoragePath, @UploadedBy, @UploadedAt);
    ";

    public const string GetById = @"
        SELECT id, organizationid, conversationid, filename, contenttype, filesize, storagepath, uploadedby, uploadedat
        FROM attachments
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";

    public const string GetByConversationId = @"
        SELECT id, organizationid, conversationid, filename, contenttype, filesize, storagepath, uploadedby, uploadedat
        FROM attachments
        WHERE conversationid = @ConversationId AND organizationid = @OrganizationId
        ORDER BY uploadedat DESC;
    ";

    public const string Delete = @"
        DELETE FROM attachments
        WHERE id = @Id AND organizationid = @OrganizationId;
    ";
}
