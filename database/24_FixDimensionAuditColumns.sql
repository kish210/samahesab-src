-- =============================================================================
-- رفعِ T1: جدول‌های ابعادِ حسابداری (CostCenters/Projects/FiscalYears) که زودتر از
-- تکمیلِ موجودیت‌ها ساخته شده‌اند، برخی ستون‌ها را ندارند → «Invalid column name …»
-- در GetCostCentersQuery/GetProjectsQuery → کرشِ صفحهٔ «ثبت سند».
-- این مهاجرت idempotent است: هر ستونِ نبوده را مطابقِ موجودیت‌های دامنه اضافه می‌کند.
-- =============================================================================
USE SamaHesab;
GO

IF OBJECT_ID('Acc.CostCenters','U') IS NOT NULL
BEGIN
    IF COL_LENGTH('Acc.CostCenters','IsActive')  IS NULL ALTER TABLE Acc.CostCenters ADD IsActive  BIT NOT NULL DEFAULT 1;
    IF COL_LENGTH('Acc.CostCenters','ParentId')  IS NULL ALTER TABLE Acc.CostCenters ADD ParentId  INT NULL;
    IF COL_LENGTH('Acc.CostCenters','CreatedAt') IS NULL ALTER TABLE Acc.CostCenters ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE();
    IF COL_LENGTH('Acc.CostCenters','UpdatedAt') IS NULL ALTER TABLE Acc.CostCenters ADD UpdatedAt DATETIME2 NULL;
END
GO

IF OBJECT_ID('Acc.Projects','U') IS NOT NULL
BEGIN
    IF COL_LENGTH('Acc.Projects','StartDate') IS NULL ALTER TABLE Acc.Projects ADD StartDate NVARCHAR(10) NULL;
    IF COL_LENGTH('Acc.Projects','EndDate')   IS NULL ALTER TABLE Acc.Projects ADD EndDate   NVARCHAR(10) NULL;
    IF COL_LENGTH('Acc.Projects','Budget')    IS NULL ALTER TABLE Acc.Projects ADD Budget    DECIMAL(18,2) NOT NULL DEFAULT 0;
    IF COL_LENGTH('Acc.Projects','IsClosed')  IS NULL ALTER TABLE Acc.Projects ADD IsClosed  BIT NOT NULL DEFAULT 0;
    IF COL_LENGTH('Acc.Projects','IsActive')  IS NULL ALTER TABLE Acc.Projects ADD IsActive  BIT NOT NULL DEFAULT 1;
    IF COL_LENGTH('Acc.Projects','CreatedAt') IS NULL ALTER TABLE Acc.Projects ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE();
    IF COL_LENGTH('Acc.Projects','UpdatedAt') IS NULL ALTER TABLE Acc.Projects ADD UpdatedAt DATETIME2 NULL;
END
GO

IF OBJECT_ID('Acc.FiscalYears','U') IS NOT NULL
BEGIN
    IF COL_LENGTH('Acc.FiscalYears','StartDate') IS NULL ALTER TABLE Acc.FiscalYears ADD StartDate NVARCHAR(10) NOT NULL DEFAULT '';
    IF COL_LENGTH('Acc.FiscalYears','EndDate')   IS NULL ALTER TABLE Acc.FiscalYears ADD EndDate   NVARCHAR(10) NOT NULL DEFAULT '';
    IF COL_LENGTH('Acc.FiscalYears','IsClosed')  IS NULL ALTER TABLE Acc.FiscalYears ADD IsClosed  BIT NOT NULL DEFAULT 0;
    IF COL_LENGTH('Acc.FiscalYears','IsActive')  IS NULL ALTER TABLE Acc.FiscalYears ADD IsActive  BIT NOT NULL DEFAULT 1;
    IF COL_LENGTH('Acc.FiscalYears','CreatedAt') IS NULL ALTER TABLE Acc.FiscalYears ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE();
    IF COL_LENGTH('Acc.FiscalYears','UpdatedAt') IS NULL ALTER TABLE Acc.FiscalYears ADD UpdatedAt DATETIME2 NULL;
END
GO

PRINT N'T1 fix: dimension audit/state columns ensured.';
GO
