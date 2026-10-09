SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Ties a "whatsapp"/"otp" API key to the WhatsApp number it sends from. NULL keeps the old behaviour
-- (the org's configured OTP number, or its only active WhatsApp number).
IF COL_LENGTH('api_keys', 'channelaccountid') IS NULL
    ALTER TABLE api_keys ADD channelaccountid UNIQUEIDENTIFIER NULL;
GO
