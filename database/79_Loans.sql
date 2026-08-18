-- 79_Loans.sql — U-LOAN: تسهیلاتِ مالی/وام (اصل/بهره/اقساط).
-- جدولِ رجیستریِ وام + جمعِ پرداختی‌ها؛ جدولِ اقساط محاسبه‌ای است (LoanCalculator) و ذخیره نمی‌شود.
-- سندِ دریافتِ وام و هر قسط توسطِ دستورهایِ Application زده می‌شود. idempotent.
IF SCHEMA_ID('Acc') IS NULL EXEC('CREATE SCHEMA Acc');
GO

IF OBJECT_ID('Acc.Loans', 'U') IS NULL
BEGIN
    CREATE TABLE Acc.Loans (
        Id                     INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId              INT            NOT NULL,
        Code                   NVARCHAR(50)   NOT NULL,
        Name                   NVARCHAR(200)  NOT NULL,
        StartDate              NVARCHAR(20)   NOT NULL,          -- شمسی «yyyy/MM/dd»
        Principal              DECIMAL(18,2)  NOT NULL,
        AnnualInterestPercent  DECIMAL(9,4)   NOT NULL CONSTRAINT DF_Loans_Rate DEFAULT(0),
        TermMonths             INT            NOT NULL,
        Status                 INT            NOT NULL CONSTRAINT DF_Loans_Status DEFAULT(1),  -- 1=فعال · 2=تسویه
        PaidInstallments       INT            NOT NULL CONSTRAINT DF_Loans_PaidCnt DEFAULT(0),
        PaidPrincipal          DECIMAL(18,2)  NOT NULL CONSTRAINT DF_Loans_PaidPrin DEFAULT(0),
        PaidInterest           DECIMAL(18,2)  NOT NULL CONSTRAINT DF_Loans_PaidInt DEFAULT(0),
        LastPaymentDate        NVARCHAR(20)   NULL,
        CreatedAt              DATETIME2      NOT NULL CONSTRAINT DF_Loans_Created DEFAULT(SYSDATETIME()),
        UpdatedAt              DATETIME2      NULL,
        CreatedByUserId        INT            NULL,
        UpdatedByUserId        INT            NULL
    );
    CREATE INDEX IX_Loans_Company ON Acc.Loans(CompanyId);
END
GO
