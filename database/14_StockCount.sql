-- =============================================================================
-- SAMA HESAB ERP — انبارگردانی سندی (Stock Count) — کار #۲۸
-- شمارش فیزیکی انبار + گزارش مغایرت + تبدیل به تعدیل موجودی.
-- idempotent — روی پایگاه‌داده‌ی موجود هم قابل اجراست.
-- =============================================================================
USE SamaHesab;
GO

IF OBJECT_ID('Inv.StockCountSessions', 'U') IS NULL
CREATE TABLE Inv.StockCountSessions (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL,
    BranchId    INT NOT NULL,
    WarehouseId INT NOT NULL,
    Date        NVARCHAR(10) NOT NULL,     -- «YYYY/MM/DD» شمسی
    Status      INT NOT NULL DEFAULT 0,    -- 0=باز 1=نهایی‌شده
    PostedAt    DATETIME2 NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt   DATETIME2
);
GO

IF OBJECT_ID('Inv.StockCountLines', 'U') IS NULL
CREATE TABLE Inv.StockCountLines (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    SessionId   INT NOT NULL REFERENCES Inv.StockCountSessions(Id),
    ProductId   INT NOT NULL,
    ProductName NVARCHAR(200) NOT NULL DEFAULT N'',
    SystemQty   DECIMAL(18,3) NOT NULL DEFAULT 0,
    CountedQty  DECIMAL(18,3) NOT NULL DEFAULT 0
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockCountLines_SessionId')
    CREATE INDEX IX_StockCountLines_SessionId ON Inv.StockCountLines(SessionId);
GO

PRINT N'انبارگردانی (Inv.StockCountSessions / Lines) با موفقیت ساخته شد.';
GO
