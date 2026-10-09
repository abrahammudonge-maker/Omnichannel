SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('api_keys', 'scope') IS NULL
    ALTER TABLE api_keys ADD scope NVARCHAR(20) NOT NULL CONSTRAINT DF_api_keys_scope DEFAULT 'integrations';
GO

IF OBJECT_ID('otp_messages', 'U') IS NULL
BEGIN
    CREATE TABLE otp_messages (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
        conversationid UNIQUEIDENTIFIER NULL REFERENCES conversations(id),
        messageid UNIQUEIDENTIFIER NULL REFERENCES messages(id),
        channelaccountid UNIQUEIDENTIFIER NULL,
        phonenumber NVARCHAR(32) NOT NULL,
        purpose NVARCHAR(100) NULL,
        idempotencykey NVARCHAR(100) NULL,
        externalmessageid NVARCHAR(200) NULL,
        status NVARCHAR(20) NOT NULL,
        expiresat DATETIMEOFFSET NOT NULL,
        createdat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_otp_messages_phone' AND object_id = OBJECT_ID('otp_messages'))
    CREATE INDEX idx_otp_messages_phone ON otp_messages(organizationid, phonenumber, createdat);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_otp_messages_external' AND object_id = OBJECT_ID('otp_messages'))
    CREATE INDEX idx_otp_messages_external ON otp_messages(organizationid, externalmessageid);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_otp_messages_idempotency' AND object_id = OBJECT_ID('otp_messages'))
    CREATE UNIQUE INDEX ux_otp_messages_idempotency ON otp_messages(organizationid, idempotencykey) WHERE idempotencykey IS NOT NULL;
GO
