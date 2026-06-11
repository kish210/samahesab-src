-- =============================================================================
-- SAMA HESAB ERP — فاکتورهای فروشِ تکرارشونده (Recurring Invoices)
-- یک فاکتور الگو + زمان‌بندی؛ موتور در سررسید، فاکتور فروش واقعی تولید می‌کند.
-- idempotent — روی پایگاه‌داده‌ی موجود هم قابل اجراست.
-- =============================================================================
USE SamaHesab;
GO

IF OBJECT_ID('Sal.RecurringInvoices', 'U') IS NULL
CREATE TABLE Sal.RecurringInvoices (
    Id                INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId         INT NOT NULL,
    BranchId          INT NOT NULL,
    Name              NVARCHAR(150) NOT NULL,
    CustomerId        INT NOT NULL,
    WarehouseId       INT NOT NULL,
    PriceLevel        NVARCHAR(20)  NOT NULL DEFAULT N'خرده',
    Frequency         INT NOT NULL DEFAULT 0,     -- 0=ماهانه 1=سالانه
    NextDate          NVARCHAR(10) NOT NULL,       -- «YYYY/MM/DD» شمسی
    LastGeneratedDate NVARCHAR(10) NULL,
    IsActive          BIT NOT NULL DEFAULT 1,
    Description       NVARCHAR(500) NULL,
    CreatedAt         DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt         DATETIME2
);
GO

IF OBJECT_ID('Sal.RecurringInvoiceLines', 'U') IS NULL
CREATE TABLE Sal.RecurringInvoiceLines (
    Id                 INT IDENTITY(1,1) PRIMARY KEY,
    RecurringInvoiceId INT NOT NULL REFERENCES Sal.RecurringInvoices(Id),
    ProductId          INT NOT NULL,
    Quantity           DECIMAL(18,3) NOT NULL,
    UnitPrice          DECIMAL(18,2) NOT NULL DEFAULT 0,
    TaxPct             DECIMAL(18,2) NOT NULL DEFAULT 0
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecurringInvoiceLines_RInvId')
CREATE INDEX IX_RecurringInvoiceLines_RInvId ON Sal.RecurringInvoiceLines(RecurringInvoiceId);
GO

PRINT N'فاکتورهای تکرارشونده (Sal.RecurringInvoices / Lines) با موفقیت ساخته شد.';
GO
