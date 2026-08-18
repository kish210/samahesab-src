-- 78_FinancialStatementNotes.sql — U-FIN-NOTES: یادداشت‌های توضیحی صورت‌های مالی.
-- ترازنامه/سودوزیان/جریان نقد هر کدام می‌توانند چند یادداشت متنی داشته باشند که در
-- خروجیِ چاپی/اکسلِ همان صورت هم ضمیمه می‌شوند. idempotent.
IF SCHEMA_ID('Acc') IS NULL EXEC('CREATE SCHEMA Acc');
GO

IF OBJECT_ID('Acc.FinancialStatementNotes', 'U') IS NULL
BEGIN
    CREATE TABLE Acc.FinancialStatementNotes (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId       INT            NOT NULL,
        StatementType   INT            NOT NULL,          -- 1=ترازنامه · 2=سودوزیان · 3=جریان نقد
        Title           NVARCHAR(200)  NOT NULL,
        Body            NVARCHAR(2000) NULL,
        [Order]         INT            NOT NULL CONSTRAINT DF_FinNotes_Order DEFAULT(0),
        CreatedAt       DATETIME2      NOT NULL CONSTRAINT DF_FinNotes_Created DEFAULT(SYSDATETIME()),
        UpdatedAt       DATETIME2      NULL,
        CreatedByUserId INT            NULL,
        UpdatedByUserId INT            NULL
    );
    CREATE INDEX IX_FinNotes_Company ON Acc.FinancialStatementNotes(CompanyId, StatementType);
END
GO
