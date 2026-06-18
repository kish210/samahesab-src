-- =============================================================================
-- 33_SalesInvoiceHeaderFields.sql — فاکتورِ فروشِ کلاسیک (سبکِ حسابفا)
-- افزودنِ «ارجاع»، «عنوان» و «پروژه» به جدولِ فاکتورهای فروش (idempotent).
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'Sal.SalesInvoices') AND name = N'Reference')
BEGIN
    ALTER TABLE Sal.SalesInvoices ADD Reference NVARCHAR(100) NULL;   -- ارجاع (شمارهٔ مرجع/سفارش)
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'Sal.SalesInvoices') AND name = N'Title')
BEGIN
    ALTER TABLE Sal.SalesInvoices ADD Title NVARCHAR(200) NULL;       -- عنوانِ فاکتور
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'Sal.SalesInvoices') AND name = N'ProjectId')
BEGIN
    ALTER TABLE Sal.SalesInvoices ADD ProjectId INT NULL;            -- پروژهٔ مرتبط
END
GO
