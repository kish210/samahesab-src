-- =============================================================================
-- PAY-C1-5 — جدولِ تنظیماتِ سالِ حقوق (نرخ‌ها و مبالغِ پایه، قابلِ‌ویرایش هر سال).
-- یک ردیف برای هر شرکت+سال. idempotent.
-- =============================================================================
USE SamaHesab;
GO

IF OBJECT_ID('Hrm.PayrollSettings', 'U') IS NULL
BEGIN
    CREATE TABLE Hrm.PayrollSettings (
        Id                     int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CompanyId              int            NOT NULL,
        Year                   nvarchar(10)   NOT NULL,
        MinWageMonthly         decimal(18,2)  NOT NULL CONSTRAINT DF_PaySet_MinWage    DEFAULT 0,
        HousingAllowance       decimal(18,2)  NOT NULL CONSTRAINT DF_PaySet_Housing    DEFAULT 0,
        FoodAllowance          decimal(18,2)  NOT NULL CONSTRAINT DF_PaySet_Food       DEFAULT 0,
        ChildAllowancePerChild decimal(18,2)  NOT NULL CONSTRAINT DF_PaySet_Child      DEFAULT 0,
        MonthlyTaxExemption    decimal(18,2)  NOT NULL CONSTRAINT DF_PaySet_TaxExempt  DEFAULT 0,
        InsuranceEmployeeRate  decimal(18,4)  NOT NULL CONSTRAINT DF_PaySet_InsEmp     DEFAULT 0.07,
        InsuranceEmployerRate  decimal(18,4)  NOT NULL CONSTRAINT DF_PaySet_InsEmpr    DEFAULT 0.23,
        HoursPerMonth          decimal(18,2)  NOT NULL CONSTRAINT DF_PaySet_Hours      DEFAULT 220,
        OvertimeFactor         decimal(18,4)  NOT NULL CONSTRAINT DF_PaySet_OT         DEFAULT 1.40,
        HolidayFactor          decimal(18,4)  NOT NULL CONSTRAINT DF_PaySet_Hol        DEFAULT 1.40,
        NightShiftFactor       decimal(18,4)  NOT NULL CONSTRAINT DF_PaySet_Night      DEFAULT 0.35,
        MaxChildren            int            NOT NULL CONSTRAINT DF_PaySet_MaxChild   DEFAULT 2,
        CONSTRAINT UQ_PayrollSettings_Company_Year UNIQUE (CompanyId, Year)
    );
END
GO

-- ستون‌های ممیزیِ AuditableEntity (EF لازم دارد) — idempotent.
IF COL_LENGTH('Hrm.PayrollSettings', 'CreatedAt') IS NULL
    ALTER TABLE Hrm.PayrollSettings ADD CreatedAt datetime NOT NULL CONSTRAINT DF_PaySet_Created DEFAULT GETDATE();
GO
IF COL_LENGTH('Hrm.PayrollSettings', 'UpdatedAt') IS NULL
    ALTER TABLE Hrm.PayrollSettings ADD UpdatedAt datetime NULL;
GO
