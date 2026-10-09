-- Merges duplicate customers that share the same WhatsApp number in different formats
-- (e.g. 0758218192 and 254758218192), and merges their conversations on the same channel account.
--
-- Runs in one transaction. @Commit = 0 (the default) reports what would change and rolls back.
-- Set @Commit = 1 only after a database backup and a reviewed dry run.

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @Commit BIT = 0;

BEGIN TRANSACTION;

-- 1. Normalize every WhatsApp number the same way the application does.
CREATE TABLE #norm (customerid UNIQUEIDENTIFIER PRIMARY KEY, organizationid UNIQUEIDENTIFIER NOT NULL, norm NVARCHAR(50) NOT NULL, createdat DATETIMEOFFSET NOT NULL);

;WITH cleaned AS (
    SELECT id, organizationid, createdat,
           REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(whatsappnumber, ' ', ''), '+', ''), '-', ''), '(', ''), ')', '') AS raw
    FROM customers
    WHERE whatsappnumber IS NOT NULL AND LTRIM(RTRIM(whatsappnumber)) <> ''
), stripped AS (
    SELECT id, organizationid, createdat,
           CASE WHEN raw LIKE '00%' THEN SUBSTRING(raw, 3, 50) ELSE raw END AS d
    FROM cleaned
)
INSERT INTO #norm (customerid, organizationid, norm, createdat)
SELECT id, organizationid,
       CASE WHEN LEN(d) = 10 AND LEFT(d, 1) = '0' THEN '254' + SUBSTRING(d, 2, 9)
            WHEN LEN(d) = 9 AND LEFT(d, 1) IN ('7', '1') THEN '254' + d
            ELSE d END,
       createdat
FROM stripped;

-- 2. The oldest customer in each number group is kept; the rest are merged into it.
CREATE TABLE #members (customerid UNIQUEIDENTIFIER PRIMARY KEY, keeperid UNIQUEIDENTIFIER NOT NULL, norm NVARCHAR(50) NOT NULL);

;WITH groups AS (
    SELECT organizationid, norm FROM #norm GROUP BY organizationid, norm HAVING COUNT(*) > 1
), ranked AS (
    SELECT n.customerid, n.norm,
           FIRST_VALUE(n.customerid) OVER (PARTITION BY n.organizationid, n.norm ORDER BY n.createdat, n.customerid) AS keeperid
    FROM #norm n
    JOIN groups g ON g.organizationid = n.organizationid AND g.norm = n.norm
)
INSERT INTO #members (customerid, keeperid, norm)
SELECT customerid, keeperid, norm FROM ranked;

CREATE TABLE #dupCustomers (customerid UNIQUEIDENTIFIER PRIMARY KEY, keeperid UNIQUEIDENTIFIER NOT NULL);
INSERT INTO #dupCustomers SELECT customerid, keeperid FROM #members WHERE customerid <> keeperid;

-- 3. Conversations of every customer in a group. Each one maps to the keeper's conversation on the
--    same channel account and channel, or to itself if it is that conversation.
CREATE TABLE #allConv (convid UNIQUEIDENTIFIER PRIMARY KEY, targetcustomer UNIQUEIDENTIFIER NOT NULL, channel SMALLINT NOT NULL, channelaccountkey NVARCHAR(40) NOT NULL, createdat DATETIMEOFFSET NOT NULL);
INSERT INTO #allConv
SELECT c.id, m.keeperid, c.channel, ISNULL(CAST(c.channelaccountid AS NVARCHAR(40)), 'none'), c.createdat
FROM conversations c
JOIN #members m ON m.customerid = c.customerid;

CREATE TABLE #convMap (fromconv UNIQUEIDENTIFIER PRIMARY KEY, toconv UNIQUEIDENTIFIER NOT NULL, targetcustomer UNIQUEIDENTIFIER NOT NULL);
;WITH ranked AS (
    SELECT convid, targetcustomer,
           FIRST_VALUE(convid) OVER (PARTITION BY targetcustomer, channel, channelaccountkey ORDER BY createdat, convid) AS toconv
    FROM #allConv
)
INSERT INTO #convMap (fromconv, toconv, targetcustomer)
SELECT convid, toconv, targetcustomer FROM ranked;

-- Report what will change.
SELECT 'Customers to merge' AS what, COUNT(*) AS total FROM #dupCustomers
UNION ALL SELECT 'Conversations to merge away', COUNT(*) FROM #convMap WHERE fromconv <> toconv
UNION ALL SELECT 'Messages to move', COUNT(*) FROM messages m JOIN #convMap c ON c.fromconv = m.conversationid WHERE c.fromconv <> c.toconv;

-- 4. Move messages into the kept conversation, then drop repeats of the same Meta message id.
UPDATE m SET m.conversationid = c.toconv
FROM messages m JOIN #convMap c ON c.fromconv = m.conversationid
WHERE c.fromconv <> c.toconv;

DELETE FROM messages
WHERE id IN (
    SELECT id FROM (
        SELECT id, ROW_NUMBER() OVER (PARTITION BY conversationid, externalmessageid ORDER BY sentat, id) AS rn
        FROM messages
        WHERE externalmessageid IS NOT NULL
          AND conversationid IN (SELECT toconv FROM #convMap)
    ) repeats
    WHERE rn > 1
);

-- 5. Re-point the other child tables that reference a conversation.
UPDATE t SET t.conversationid = c.toconv FROM conversation_assignments t JOIN #convMap c ON c.fromconv = t.conversationid WHERE c.fromconv <> c.toconv;
UPDATE t SET t.conversationid = c.toconv FROM conversation_status_history t JOIN #convMap c ON c.fromconv = t.conversationid WHERE c.fromconv <> c.toconv;
UPDATE t SET t.conversationid = c.toconv FROM attachments t JOIN #convMap c ON c.fromconv = t.conversationid WHERE c.fromconv <> c.toconv;
UPDATE t SET t.conversationid = c.toconv FROM internal_notes t JOIN #convMap c ON c.fromconv = t.conversationid WHERE c.fromconv <> c.toconv;
UPDATE t SET t.conversationid = c.toconv FROM notifications t JOIN #convMap c ON c.fromconv = t.conversationid WHERE c.fromconv <> c.toconv;

IF OBJECT_ID('calls', 'U') IS NOT NULL
    EXEC ('UPDATE t SET t.conversationid = c.toconv FROM calls t JOIN #convMap c ON c.fromconv = t.conversationid WHERE c.fromconv <> c.toconv');
IF OBJECT_ID('otp_messages', 'U') IS NOT NULL
    EXEC ('UPDATE t SET t.conversationid = c.toconv FROM otp_messages t JOIN #convMap c ON c.fromconv = t.conversationid WHERE c.fromconv <> c.toconv');

-- 6. Tags: copy the ones the kept conversation doesn't have yet, then drop the merged-away rows.
INSERT INTO conversation_tags (conversationid, tagid)
SELECT DISTINCT c.toconv, ct.tagid
FROM conversation_tags ct
JOIN #convMap c ON c.fromconv = ct.conversationid
WHERE c.fromconv <> c.toconv
  AND NOT EXISTS (SELECT 1 FROM conversation_tags x WHERE x.conversationid = c.toconv AND x.tagid = ct.tagid);

DELETE ct FROM conversation_tags ct JOIN #convMap c ON c.fromconv = ct.conversationid WHERE c.fromconv <> c.toconv;

-- 7. The kept conversations now belong to the kept customer; merged-away conversations are removed.
UPDATE c SET c.customerid = m.targetcustomer
FROM conversations c JOIN #convMap m ON m.toconv = c.id;

DELETE FROM conversations WHERE id IN (SELECT fromconv FROM #convMap WHERE fromconv <> toconv);

IF OBJECT_ID('calls', 'U') IS NOT NULL
    EXEC ('UPDATE t SET t.customerid = d.keeperid FROM calls t JOIN #dupCustomers d ON d.customerid = t.customerid');

-- 8. Keep the kept customer's number in the normalized form, then remove the duplicate customers.
UPDATE cu SET cu.whatsappnumber = n.norm
FROM customers cu JOIN #norm n ON n.customerid = cu.id
WHERE cu.id IN (SELECT keeperid FROM #dupCustomers);

DELETE FROM customers WHERE id IN (SELECT customerid FROM #dupCustomers);

-- Final result.
SELECT 'Customers merged' AS what, (SELECT COUNT(*) FROM #dupCustomers) AS total
UNION ALL SELECT 'Conversations merged away', (SELECT COUNT(*) FROM #convMap WHERE fromconv <> toconv);

IF @Commit = 1
BEGIN
    COMMIT TRANSACTION;
    SELECT 'COMMITTED' AS result;
END
ELSE
BEGIN
    ROLLBACK TRANSACTION;
    SELECT 'DRY RUN - nothing was changed. Set @Commit = 1 to apply.' AS result;
END
