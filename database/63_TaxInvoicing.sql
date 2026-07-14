-- =============================================================================
-- ماژولِ سامانهٔ مودیان — اسکیمای Tax. ارسالِ الکترونیکیِ فاکتورِ فروش به سازمانِ امورِ مالیاتی.
-- رفرنسِ نرم به SalesInvoiceId/ProductId (بدونِ FK به schemaی هسته — قاعدهٔ removability: حذفِ این
-- ماژول نباید هستهٔ فروش را بشکند و برعکس). idempotent؛ GO-split؛ بدونِ USE.
-- =============================================================================

IF SCHEMA_ID('Tax') IS NULL EXEC('CREATE SCHEMA Tax');
GO

-- ── رکوردِ ارسالِ هر فاکتور (store-and-forward) ──
IF OBJECT_ID('Tax.Submissions', 'U') IS NULL
CREATE TABLE Tax.Submissions (
    Id              int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId       int           NOT NULL,
    SalesInvoiceId  int           NOT NULL,
    Status          nvarchar(20)  NOT NULL CONSTRAINT DF_TaxSub_Status DEFAULT N'Pending',
    UniqueTaxId     nvarchar(30)  NULL,
    ReferenceNumber nvarchar(60)  NULL,
    ErrorMessage    nvarchar(1000) NULL,
    RetryCount      int           NOT NULL CONSTRAINT DF_TaxSub_Retry DEFAULT 0,
    SentAt          datetime      NULL,
    LastAttemptAt   datetime      NULL,
    CreatedAt       datetime      NOT NULL CONSTRAINT DF_TaxSub_Created DEFAULT GETDATE(),
    UpdatedAt       datetime      NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TaxSub_Company_Invoice' AND object_id=OBJECT_ID('Tax.Submissions'))
    CREATE INDEX IX_TaxSub_Company_Invoice ON Tax.Submissions (CompanyId, SalesInvoiceId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TaxSub_Company_Status' AND object_id=OBJECT_ID('Tax.Submissions'))
    CREATE INDEX IX_TaxSub_Company_Status ON Tax.Submissions (CompanyId, Status);
GO

-- ── نگاشتِ کالا→شناسهٔ رسمی/واحدِ اندازه‌گیریِ سامانهٔ مودیان ──
IF OBJECT_ID('Tax.ItemCodes', 'U') IS NULL
CREATE TABLE Tax.ItemCodes (
    Id                  int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId           int           NOT NULL,
    ProductId           int           NOT NULL,
    ItemId              nvarchar(50)  NOT NULL,
    MeasurementUnitCode nvarchar(20)  NOT NULL,
    CreatedAt           datetime      NOT NULL CONSTRAINT DF_TaxItm_Created DEFAULT GETDATE(),
    UpdatedAt           datetime      NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TaxItm_Company_Product' AND object_id=OBJECT_ID('Tax.ItemCodes'))
    CREATE UNIQUE INDEX IX_TaxItm_Company_Product ON Tax.ItemCodes (CompanyId, ProductId);
GO
