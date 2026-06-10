-- =============================================================================
-- SAMA HESAB ERP — الگوهای سند حسابداری (Voucher Templates)
-- بهره‌وری: ساخت سریع سند پیش‌نویس از روی الگوی ازپیش‌تعریف‌شده.
-- idempotent — روی پایگاه‌داده‌ی موجود هم قابل اجراست.
-- =============================================================================
USE SamaHesab;
GO

IF OBJECT_ID('Acc.VoucherTemplates', 'U') IS NULL
CREATE TABLE Acc.VoucherTemplates (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId     INT NOT NULL,
    BranchId      INT NOT NULL,
    Name          NVARCHAR(150) NOT NULL,
    Description   NVARCHAR(500) NULL,
    VoucherTypeId INT NOT NULL DEFAULT 1,
    IsActive      BIT NOT NULL DEFAULT 1,
    CreatedAt     DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt     DATETIME2
);
GO

IF OBJECT_ID('Acc.VoucherTemplateLines', 'U') IS NULL
CREATE TABLE Acc.VoucherTemplateLines (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    TemplateId  INT NOT NULL REFERENCES Acc.VoucherTemplates(Id),
    RowNumber   INT NOT NULL,
    AccountId   INT NOT NULL,
    Debit       DECIMAL(18,2) NOT NULL DEFAULT 0,
    Credit      DECIMAL(18,2) NOT NULL DEFAULT 0,
    Description NVARCHAR(300) NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VoucherTemplateLines_TemplateId')
    CREATE INDEX IX_VoucherTemplateLines_TemplateId ON Acc.VoucherTemplateLines(TemplateId);
GO

PRINT N'الگوهای سند (Acc.VoucherTemplates) با موفقیت ساخته شد.';
GO
