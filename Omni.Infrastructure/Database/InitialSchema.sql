-- Initial schema for the Omnichannel platform (SQL Server)
-- Run against the database configured in ConnectionStrings:DefaultConnection.
-- Column names are lowercase/unquoted to match the raw SQL in Omni.Infrastructure/Queries.
-- Foreign keys are NO ACTION (not CASCADE) to avoid SQL Server's multiple-cascade-path restriction
-- given how deeply nested these tables are (organizationid + conversationid both trace back to organizations).

IF DB_ID('omnichannel') IS NULL
BEGIN
    CREATE DATABASE omnichannel;
END
GO

USE omnichannel;
GO

CREATE TABLE organizations (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    name NVARCHAR(200) NOT NULL,
    email NVARCHAR(200) NOT NULL,
    phone NVARCHAR(50) NOT NULL,
    country NVARCHAR(100) NOT NULL,
    status NVARCHAR(50) NOT NULL DEFAULT 'Active',
    createdat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
GO

CREATE TABLE users (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
    firstname NVARCHAR(100) NOT NULL,
    lastname NVARCHAR(100) NOT NULL,
    email NVARCHAR(200) NOT NULL UNIQUE,
    passwordhash NVARCHAR(300) NOT NULL,
    role SMALLINT NOT NULL,
    isactive BIT NOT NULL DEFAULT 1,
    createdat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
CREATE INDEX idx_users_organizationid ON users(organizationid);
GO

CREATE TABLE departments (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
    name NVARCHAR(200) NOT NULL,
    description NVARCHAR(MAX),
    isactive BIT NOT NULL DEFAULT 1,
    createdat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    updatedat DATETIMEOFFSET
);
CREATE INDEX idx_departments_organizationid ON departments(organizationid);
GO

CREATE TABLE teams (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
    departmentid UNIQUEIDENTIFIER NOT NULL REFERENCES departments(id),
    name NVARCHAR(200) NOT NULL,
    description NVARCHAR(MAX),
    leaderid UNIQUEIDENTIFIER REFERENCES users(id),
    isactive BIT NOT NULL DEFAULT 1,
    createdat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
CREATE INDEX idx_teams_organizationid ON teams(organizationid);
CREATE INDEX idx_teams_departmentid ON teams(departmentid);
GO

CREATE TABLE customers (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
    fullname NVARCHAR(200) NOT NULL,
    phone NVARCHAR(50) NOT NULL,
    email NVARCHAR(200) NOT NULL,
    facebookid NVARCHAR(200),
    instagramid NVARCHAR(200),
    whatsappnumber NVARCHAR(50),
    createdat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
CREATE INDEX idx_customers_organizationid ON customers(organizationid);
GO

CREATE TABLE conversations (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
    customerid UNIQUEIDENTIFIER NOT NULL REFERENCES customers(id),
    channel SMALLINT NOT NULL,
    status NVARCHAR(50) NOT NULL DEFAULT 'Open',
    assigneduserid UNIQUEIDENTIFIER REFERENCES users(id),
    createdat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
CREATE INDEX idx_conversations_organizationid ON conversations(organizationid);
CREATE INDEX idx_conversations_customerid ON conversations(customerid);
GO

CREATE TABLE conversation_assignments (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    conversationid UNIQUEIDENTIFIER NOT NULL REFERENCES conversations(id),
    assignedto UNIQUEIDENTIFIER NOT NULL REFERENCES users(id),
    assignedby UNIQUEIDENTIFIER NOT NULL REFERENCES users(id),
    assignedat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    reason NVARCHAR(MAX)
);
CREATE INDEX idx_conversation_assignments_conversationid ON conversation_assignments(conversationid);
GO

CREATE TABLE conversation_status_history (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
    conversationid UNIQUEIDENTIFIER NOT NULL REFERENCES conversations(id),
    status NVARCHAR(50) NOT NULL,
    changedby UNIQUEIDENTIFIER REFERENCES users(id),
    changedat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    reason NVARCHAR(MAX)
);
CREATE INDEX idx_conversation_status_history_conversationid ON conversation_status_history(conversationid);
GO

CREATE TABLE messages (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
    conversationid UNIQUEIDENTIFIER NOT NULL REFERENCES conversations(id),
    direction NVARCHAR(20) NOT NULL DEFAULT 'Inbound',
    messagetype NVARCHAR(20) NOT NULL DEFAULT 'Text',
    body NVARCHAR(MAX) NOT NULL,
    attachmenturl NVARCHAR(500),
    sentat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    status NVARCHAR(20) NOT NULL DEFAULT 'Sent'
);
CREATE INDEX idx_messages_organizationid ON messages(organizationid);
CREATE INDEX idx_messages_conversationid ON messages(conversationid);
GO

CREATE TABLE tags (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
    name NVARCHAR(100) NOT NULL,
    description NVARCHAR(MAX),
    isactive BIT NOT NULL DEFAULT 1,
    createdat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
CREATE INDEX idx_tags_organizationid ON tags(organizationid);
GO

CREATE TABLE conversation_tags (
    conversationid UNIQUEIDENTIFIER NOT NULL REFERENCES conversations(id),
    tagid UNIQUEIDENTIFIER NOT NULL REFERENCES tags(id),
    PRIMARY KEY (conversationid, tagid)
);
GO

CREATE TABLE attachments (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
    conversationid UNIQUEIDENTIFIER NOT NULL REFERENCES conversations(id),
    filename NVARCHAR(300) NOT NULL,
    contenttype NVARCHAR(150) NOT NULL,
    filesize BIGINT NOT NULL,
    storagepath NVARCHAR(500) NOT NULL,
    uploadedby UNIQUEIDENTIFIER NOT NULL REFERENCES users(id),
    uploadedat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
CREATE INDEX idx_attachments_conversationid ON attachments(conversationid);
GO

CREATE TABLE internal_notes (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
    conversationid UNIQUEIDENTIFIER NOT NULL REFERENCES conversations(id),
    userid UNIQUEIDENTIFIER NOT NULL REFERENCES users(id),
    body NVARCHAR(MAX) NOT NULL,
    createdat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    editedat DATETIMEOFFSET
);
CREATE INDEX idx_internal_notes_conversationid ON internal_notes(conversationid);
GO

CREATE TABLE notifications (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
    userid UNIQUEIDENTIFIER NOT NULL REFERENCES users(id),
    title NVARCHAR(200) NOT NULL,
    message NVARCHAR(MAX) NOT NULL,
    isread BIT NOT NULL DEFAULT 0,
    createdat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
CREATE INDEX idx_notifications_userid ON notifications(userid);
GO

CREATE TABLE audit_logs (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
    userid UNIQUEIDENTIFIER REFERENCES users(id),
    action NVARCHAR(100) NOT NULL,
    entity NVARCHAR(100) NOT NULL,
    entityid UNIQUEIDENTIFIER,
    ipaddress NVARCHAR(50),
    [timestamp] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    metadata NVARCHAR(MAX)
);
CREATE INDEX idx_audit_logs_organizationid ON audit_logs(organizationid);
GO

CREATE TABLE organization_settings (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
    settingname NVARCHAR(150) NOT NULL,
    settingvalue NVARCHAR(MAX) NOT NULL
);
CREATE UNIQUE INDEX idx_organization_settings_org_name ON organization_settings(organizationid, settingname);
GO

CREATE TABLE channel_accounts (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    organizationid UNIQUEIDENTIFIER NOT NULL REFERENCES organizations(id),
    channeltype NVARCHAR(50) NOT NULL,
    displayname NVARCHAR(200) NOT NULL,
    externalaccountid NVARCHAR(200),
    accesstoken NVARCHAR(MAX),
    refreshtoken NVARCHAR(MAX),
    webhooksecret NVARCHAR(300),
    status NVARCHAR(50) NOT NULL DEFAULT 'Active',
    createdat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    updatedat DATETIMEOFFSET
);
CREATE INDEX idx_channel_accounts_organizationid ON channel_accounts(organizationid);
GO
