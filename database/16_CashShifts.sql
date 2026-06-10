-- =============================================================================
-- SAMA HESAB ERP — شیفت/صندوق POS (Cash Shifts) — کار #۳۰
-- باز/بستن صندوق + جمع فروش نقد/کارت + مغایرت شمارش. idempotent.
-- =============================================================================
USE SamaHesab;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Pos')
    EXEC('CREATE SCHEMA Pos');
GO

IF OBJECT_ID('Pos.CashShifts', 'U') IS NULL
CREATE TABLE Pos.CashShifts (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId    INT NOT NULL,
    BranchId     INT NOT NULL,
    UserId       INT NOT NULL,
    OpenedAt     DATETIME2 NOT NULL DEFAULT GETDATE(),
    ClosedAt     DATETIME2 NULL,
    Status       INT NOT NULL DEFAULT 0,   -- 0=باز 1=بسته
    OpeningFloat DECIMAL(18,2) NOT NULL DEFAULT 0,
    CashSales    DECIMAL(18,2) NOT NULL DEFAULT 0,
    CardSales    DECIMAL(18,2) NOT NULL DEFAULT 0,
    SalesCount   INT NOT NULL DEFAULT 0,
    CountedCash  DECIMAL(18,2) NOT NULL DEFAULT 0,
    ExpectedCash DECIMAL(18,2) NOT NULL DEFAULT 0,
    Variance     DECIMAL(18,2) NOT NULL DEFAULT 0,
    Notes        NVARCHAR(500) NULL,
    CreatedAt    DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt    DATETIME2
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CashShifts_User_Status')
    CREATE INDEX IX_CashShifts_User_Status ON Pos.CashShifts(CompanyId, UserId, Status);
GO

PRINT N'شیفت/صندوق POS (Pos.CashShifts) با موفقیت ساخته شد.';
GO
