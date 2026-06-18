-- =============================================================================
-- 34_DropOrphanTables.sql — پاکسازیِ جداولِ یتیم (طرحِ رهاشده، بدونِ موجودیت/کد)
-- ⚠️ امن: هر جدول فقط وقتی DROP می‌شود که (۱) خالی باشد و (۲) هیچ کلیدِ خارجیِ
--     ورودی نداشته باشد. پس روی نصب‌های موجود هیچ داده‌ای از بین نمی‌رود و خطا
--     رخ نمی‌دهد؛ روی نصبِ تازه این جداولِ بی‌استفاده حذف می‌شوند.
-- بررسی: این جداول نه موجودیتِ EF دارند، نه در کدِ #C ارجاع می‌شوند، نه در SQLِ خام.
-- =============================================================================

SET NOCOUNT ON;

DECLARE @orphans TABLE (FullName NVARCHAR(200));
INSERT INTO @orphans (FullName) VALUES
    (N'Acc.AccountGroups'),
    (N'Acc.CashRegisters'),
    (N'Cfg.NumberingTemplates'),
    (N'Cfg.SmsLogs'),
    (N'Cfg.SmsTemplates'),
    (N'Crm.CustomerGroups'),
    (N'Hrm.Commissions'),
    (N'Hrm.Positions'),
    (N'Inv.StockTransactionTypes'),
    (N'Pos.PosDevices'),
    (N'Pos.PosSessions'),
    (N'Pos.PosTransactions'),
    (N'Pur.PurchasePayments'),
    (N'Sal.InvoiceStatuses'),
    (N'Sal.Quotations'),
    (N'Sec.UserSessions');

DECLARE @name NVARCHAR(200), @oid INT, @rows BIGINT, @fk INT, @sql NVARCHAR(400);
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT FullName FROM @orphans;
OPEN cur;
FETCH NEXT FROM cur INTO @name;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @oid = OBJECT_ID(@name);
    IF @oid IS NOT NULL
    BEGIN
        -- تعدادِ ردیف‌ها (از متادیتا؛ سریع)
        SELECT @rows = SUM(p.rows) FROM sys.partitions p WHERE p.object_id = @oid AND p.index_id IN (0,1);
        -- کلیدهای خارجیِ ورودی
        SELECT @fk = COUNT(*) FROM sys.foreign_keys WHERE referenced_object_id = @oid;

        IF ISNULL(@rows,0) = 0 AND ISNULL(@fk,0) = 0
        BEGIN
            SET @sql = N'DROP TABLE ' + @name + N';';
            EXEC sp_executesql @sql;
            PRINT N'حذف شد: ' + @name;
        END
        ELSE
            PRINT N'رد شد (داده/FK دارد): ' + @name;
    END
    FETCH NEXT FROM cur INTO @name;
END
CLOSE cur; DEALLOCATE cur;
GO
