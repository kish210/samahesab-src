-- 71_WarehouseInventoryAccount.sql — U-INV-ACCT-WH (backlog #7): حسابِ موجودیِ GLِ اختصاصیِ هر انبار
-- (Inv.Warehouses.InventoryAccountId). null = سازگارِ عقب‌رو با حسابِ مشترکِ پیش‌فرضِ شرکت (1-05-001)
-- — یعنی انبارهایِ موجود بدونِ تغییرِ رفتار می‌مانند تا کاربر صریحاً حسابِ اختصاصی تعیین کند.
-- idempotent — توسطِ DatabaseMigrator در استارت‌آپ اجرا می‌شود.
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Inv')
   AND EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
               WHERE s.name = 'Inv' AND t.name = 'Warehouses')
   AND NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID('Inv.Warehouses') AND name = 'InventoryAccountId')
BEGIN
    ALTER TABLE Inv.Warehouses ADD InventoryAccountId INT NULL;
END
GO
