-- =============================================================================
-- SAMA HESAB ERP — ابعاد حسابداری: سال مالی · مرکز هزینه · پروژه (هستهٔ ERP)
-- سال مالی به‌عنوان موجودیتِ درجه‌یک + ابعاد تحلیلیِ سند روی VoucherItems.
-- idempotent — روی پایگاه‌داده‌ی موجود هم قابل اجراست.
-- =============================================================================
USE SamaHesab;
GO

-- ── سال مالی ─────────────────────────────────────────────────────────────────
IF OBJECT_ID('Acc.FiscalYears', 'U') IS NULL
CREATE TABLE Acc.FiscalYears (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId  INT NOT NULL,
    Title      NVARCHAR(50) NOT NULL,
    StartDate  NVARCHAR(10) NOT NULL,
    EndDate    NVARCHAR(10) NOT NULL,
    IsClosed   BIT NOT NULL DEFAULT 0,
    IsActive   BIT NOT NULL DEFAULT 1,
    CreatedAt  DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt  DATETIME2
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_FiscalYears_Company_Title')
    CREATE UNIQUE INDEX UX_FiscalYears_Company_Title ON Acc.FiscalYears(CompanyId, Title);
GO

-- ── مرکز هزینه (سلسله‌مراتبی) ─────────────────────────────────────────────────
IF OBJECT_ID('Acc.CostCenters', 'U') IS NULL
CREATE TABLE Acc.CostCenters (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId  INT NOT NULL,
    Code       NVARCHAR(30) NOT NULL,
    Name       NVARCHAR(150) NOT NULL,
    ParentId   INT NULL REFERENCES Acc.CostCenters(Id),
    IsActive   BIT NOT NULL DEFAULT 1,
    CreatedAt  DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt  DATETIME2
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_CostCenters_Company_Code')
    CREATE UNIQUE INDEX UX_CostCenters_Company_Code ON Acc.CostCenters(CompanyId, Code);
GO

-- ── پروژه ────────────────────────────────────────────────────────────────────
IF OBJECT_ID('Acc.Projects', 'U') IS NULL
CREATE TABLE Acc.Projects (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId  INT NOT NULL,
    Code       NVARCHAR(30) NOT NULL,
    Name       NVARCHAR(150) NOT NULL,
    StartDate  NVARCHAR(10) NULL,
    EndDate    NVARCHAR(10) NULL,
    Budget     DECIMAL(18,2) NOT NULL DEFAULT 0,
    IsClosed   BIT NOT NULL DEFAULT 0,
    IsActive   BIT NOT NULL DEFAULT 1,
    CreatedAt  DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt  DATETIME2
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Projects_Company_Code')
    CREATE UNIQUE INDEX UX_Projects_Company_Code ON Acc.Projects(CompanyId, Code);
GO

-- ── ابعاد روی ردیف سند (در صورت نبودِ ستون‌ها افزوده شوند) ─────────────────────
IF COL_LENGTH('Acc.VoucherItems', 'CostCenterId') IS NULL
    ALTER TABLE Acc.VoucherItems ADD CostCenterId INT NULL;
GO
IF COL_LENGTH('Acc.VoucherItems', 'ProjectId') IS NULL
    ALTER TABLE Acc.VoucherItems ADD ProjectId INT NULL;
GO

PRINT N'ابعاد حسابداری (FiscalYears/CostCenters/Projects) با موفقیت ساخته شد.';
GO
