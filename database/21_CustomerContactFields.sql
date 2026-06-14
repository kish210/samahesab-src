-- =============================================================================
-- 21_CustomerContactFields.sql — کارِ ۱۰ (تکمیل کارت مشتری، لِین C2)
-- افزودنِ «شخصِ رابط» و «ویزیتور» به جدولِ مشتریان (idempotent).
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'Crm.Customers') AND name = N'ContactPerson')
BEGIN
    ALTER TABLE Crm.Customers ADD ContactPerson NVARCHAR(100) NULL;   -- شخصِ رابط
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'Crm.Customers') AND name = N'Visitor')
BEGIN
    ALTER TABLE Crm.Customers ADD Visitor NVARCHAR(100) NULL;         -- ویزیتور/بازاریاب
END
GO
