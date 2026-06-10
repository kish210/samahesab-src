-- =============================================================================
-- SAMA HESAB ERP — اسناد تکرارشونده (Recurring Vouchers)
-- یک الگوی سند + زمان‌بندی؛ موتور تولید در سررسید سند پیش‌نویس می‌سازد.
-- idempotent — روی پایگاه‌داده‌ی موجود هم قابل اجراست.
-- =============================================================================
USE SamaHesab;
GO

IF OBJECT_ID('Acc.RecurringVouchers', 'U') IS NULL
CREATE TABLE Acc.RecurringVouchers (
    Id                INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId         INT NOT NULL,
    BranchId          INT NOT NULL,
    TemplateId        INT NOT NULL REFERENCES Acc.VoucherTemplates(Id),
    Name              NVARCHAR(150) NOT NULL,
    Frequency         INT NOT NULL DEFAULT 0,   -- 0=ماهانه 1=سالانه
    NextDate          NVARCHAR(10) NOT NULL,     -- «YYYY/MM/DD» شمسی
    LastGeneratedDate NVARCHAR(10) NULL,
    IsActive          BIT NOT NULL DEFAULT 1,
    CreatedAt         DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt         DATETIME2
);
GO

PRINT N'اسناد تکرارشونده (Acc.RecurringVouchers) با موفقیت ساخته شد.';
GO
