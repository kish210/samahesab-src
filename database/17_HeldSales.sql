-- =============================================================================
-- SAMA HESAB ERP — فاکتورهای معلق POS (Hold/Recall) — کار #۳۳
-- =============================================================================
USE SamaHesab;
GO
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Pos') EXEC('CREATE SCHEMA Pos');
GO
IF OBJECT_ID('Pos.HeldSales', 'U') IS NULL
CREATE TABLE Pos.HeldSales (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId INT NOT NULL,
    BranchId  INT NOT NULL,
    UserId    INT NOT NULL,
    Label     NVARCHAR(100) NOT NULL,
    Payload   NVARCHAR(MAX) NOT NULL,
    Total     DECIMAL(18,2) NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2
);
GO
PRINT N'فاکتورهای معلق (Pos.HeldSales) ساخته شد.';
GO
