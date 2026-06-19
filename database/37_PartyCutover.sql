-- =============================================================================
-- 37_PartyCutover.sql — Cutover نهاییِ طرف‌حساب (مرحلهٔ C)
-- FKهای فاکتور/سند/چک/... از Customers/Suppliers به Crm.Parties منتقل (repoint) می‌شوند،
-- سپس جداولِ Crm.Customers و Crm.Suppliers حذف می‌شوند. منبعِ واحد = Crm.Parties.
-- run-once: کلِ منطق در گاردِ «اگر Customers هنوز هست» است؛ پس از حذف، اجرای دوباره بی‌اثر است.
-- نگاشتِ id از طریقِ Party.LegacyCustomerId / LegacySupplierId.
-- =============================================================================

IF OBJECT_ID(N'Crm.Customers') IS NOT NULL
BEGIN
    -- ۱) حذفِ همهٔ کلیدهای خارجی که به Customers یا Suppliers اشاره می‌کنند
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.'
                       + QUOTENAME(OBJECT_NAME(fk.parent_object_id)) + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + CHAR(10)
    FROM sys.foreign_keys fk
    WHERE fk.referenced_object_id IN (OBJECT_ID(N'Crm.Customers'), OBJECT_ID(N'Crm.Suppliers'));
    IF LEN(@sql) > 0 EXEC sp_executesql @sql;

    -- ۲) repoint ستون‌های CustomerId → Party.Id (از طریقِ LegacyCustomerId)
    IF OBJECT_ID(N'Sal.SalesInvoices') IS NOT NULL
        UPDATE t SET t.CustomerId = p.Id FROM Sal.SalesInvoices t
        JOIN Crm.Parties p ON p.LegacyCustomerId = t.CustomerId;
    IF OBJECT_ID(N'Crm.CustomerAttachments') IS NOT NULL
        UPDATE t SET t.CustomerId = p.Id FROM Crm.CustomerAttachments t
        JOIN Crm.Parties p ON p.LegacyCustomerId = t.CustomerId;
    IF OBJECT_ID(N'Crm.CustomerContacts') IS NOT NULL
        UPDATE t SET t.CustomerId = p.Id FROM Crm.CustomerContacts t
        JOIN Crm.Parties p ON p.LegacyCustomerId = t.CustomerId;
    IF OBJECT_ID(N'Crm.LoyaltyTransactions') IS NOT NULL
        UPDATE t SET t.CustomerId = p.Id FROM Crm.LoyaltyTransactions t
        JOIN Crm.Parties p ON p.LegacyCustomerId = t.CustomerId;
    IF OBJECT_ID(N'Sal.RecurringInvoices') IS NOT NULL AND COL_LENGTH('Sal.RecurringInvoices','CustomerId') IS NOT NULL
        UPDATE t SET t.CustomerId = p.Id FROM Sal.RecurringInvoices t
        JOIN Crm.Parties p ON p.LegacyCustomerId = t.CustomerId;
    IF OBJECT_ID(N'Rst.RestaurantOrders') IS NOT NULL AND COL_LENGTH('Rst.RestaurantOrders','CustomerId') IS NOT NULL
        UPDATE t SET t.CustomerId = p.Id FROM Rst.RestaurantOrders t
        JOIN Crm.Parties p ON p.LegacyCustomerId = t.CustomerId;
    IF OBJECT_ID(N'Sal.Quotations') IS NOT NULL AND COL_LENGTH('Sal.Quotations','CustomerId') IS NOT NULL
        UPDATE t SET t.CustomerId = p.Id FROM Sal.Quotations t
        JOIN Crm.Parties p ON p.LegacyCustomerId = t.CustomerId;
    IF OBJECT_ID(N'Pos.PosTransactions') IS NOT NULL AND COL_LENGTH('Pos.PosTransactions','CustomerId') IS NOT NULL
        UPDATE t SET t.CustomerId = p.Id FROM Pos.PosTransactions t
        JOIN Crm.Parties p ON p.LegacyCustomerId = t.CustomerId;

    -- ۳) repoint ستون‌های SupplierId → Party.Id (از طریقِ LegacySupplierId)
    IF OBJECT_ID(N'Pur.PurchaseInvoices') IS NOT NULL
        UPDATE t SET t.SupplierId = p.Id FROM Pur.PurchaseInvoices t
        JOIN Crm.Parties p ON p.LegacySupplierId = t.SupplierId;
    IF OBJECT_ID(N'Pur.PurchaseOrders') IS NOT NULL
        UPDATE t SET t.SupplierId = p.Id FROM Pur.PurchaseOrders t
        JOIN Crm.Parties p ON p.LegacySupplierId = t.SupplierId;

    -- ۴) چک: PartyId بر اساسِ نوع (دریافتی→مشتری، پرداختی→تأمین‌کننده)
    IF OBJECT_ID(N'Acc.Cheques') IS NOT NULL AND COL_LENGTH('Acc.Cheques','PartyId') IS NOT NULL
    BEGIN
        UPDATE ch SET ch.PartyId = p.Id FROM Acc.Cheques ch
        JOIN Crm.Parties p ON p.LegacyCustomerId = ch.PartyId WHERE ch.PartyId IS NOT NULL;
        UPDATE ch SET ch.PartyId = p.Id FROM Acc.Cheques ch
        JOIN Crm.Parties p ON p.LegacySupplierId = ch.PartyId WHERE ch.PartyId IS NOT NULL;
    END

    -- ۵) حذفِ جداولِ قدیمی (داده در Crm.Parties است)
    DROP TABLE Crm.Customers;
    DROP TABLE Crm.Suppliers;

    PRINT N'Party cutover: Customers/Suppliers حذف شدند؛ FKها به Crm.Parties منتقل شد.';
END
GO
