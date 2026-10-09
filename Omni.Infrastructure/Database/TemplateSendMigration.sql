SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Durable queue for template sends made with a "whatsapp" API key. The API only validates and inserts
-- rows; TemplateSendDispatcher sends them, retries transient Meta failures, and the Meta status webhook
-- moves them on through sent -> delivered -> read (or failed).
IF OBJECT_ID('template_sends', 'U') IS NULL
BEGIN
    CREATE TABLE template_sends (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
        apikeyid UNIQUEIDENTIFIER NULL,
        batchid UNIQUEIDENTIFIER NOT NULL,
        channelaccountid UNIQUEIDENTIFIER NOT NULL,
        templateid UNIQUEIDENTIFIER NOT NULL,
        phonenumber NVARCHAR(32) NOT NULL,
        customername NVARCHAR(200) NULL,
        bodyparametersjson NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        idempotencykey NVARCHAR(100) NULL,
        status NVARCHAR(20) NOT NULL,
        attempts INT NOT NULL DEFAULT 0,
        nextattemptat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        lockedat DATETIMEOFFSET NULL,
        conversationid UNIQUEIDENTIFIER NULL,
        messageid UNIQUEIDENTIFIER NULL,
        externalmessageid NVARCHAR(200) NULL,
        error NVARCHAR(1000) NULL,
        createdat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        updatedat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_template_sends_due' AND object_id = OBJECT_ID('template_sends'))
    CREATE INDEX idx_template_sends_due ON template_sends(status, nextattemptat);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_template_sends_batch' AND object_id = OBJECT_ID('template_sends'))
    CREATE INDEX idx_template_sends_batch ON template_sends(organizationid, batchid);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_template_sends_external' AND object_id = OBJECT_ID('template_sends'))
    CREATE INDEX idx_template_sends_external ON template_sends(organizationid, externalmessageid);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_template_sends_idempotency' AND object_id = OBJECT_ID('template_sends'))
    CREATE UNIQUE INDEX ux_template_sends_idempotency ON template_sends(organizationid, idempotencykey) WHERE idempotencykey IS NOT NULL;
GO
