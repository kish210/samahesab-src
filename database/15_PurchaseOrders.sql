-- =============================================================================
-- SAMA HESAB ERP — سفارش خرید (Purchase Orders)
-- درخواست تأمین کالا پیش از فاکتور خرید؛ قابل ساخت دستی یا خودکار از نقطه‌ی سفارش.
-- idempotent — روی پایگاه‌داده‌ی موجود هم قابل اجراست.
-- =============================================================================
USE SamaHesab;
GO

IF SCHEMA_ID('Pur') IS NULL EXEC('CREATE SCHEMA Pur');
GO

IF OBJECT_ID('Pur.PurchaseOrders', 'U') IS NULL
CREATE TABLE Pur.PurchaseOrders (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId    INT NOT NULL,
    BranchId     INT NOT NULL,
    OrderNumber  NVARCHAR(50)  NOT NULL,
    OrderDate    NVARCHAR(10)  NOT NULL,        -- «YYYY/MM/DD» شمسی
    SupplierId   INT NULL,
    StatusCode   NVARCHAR(20)  NOT NULL DEFAULT N'پیش‌نویس',
    Source       NVARCHAR(20)  NOT NULL DEFAULT N'دستی',   -- دستی / خودکار
    Description  NVARCHAR(500) NULL,
    Total        DECIMAL(18,2) NOT NULL DEFAULT 0,
    CreatedAt    DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt    DATETIME2
);
GO

IF OBJECT_ID('Pur.PurchaseOrderItems', 'U') IS NULL
CREATE TABLE Pur.PurchaseOrderItems (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    OrderId    INT NOT NULL REFERENCES Pur.PurchaseOrders(Id),
    RowNumber  INT NOT NULL,
    ProductId  INT NOT NULL,
    Quantity   DECIMAL(18,3) NOT NULL,
    UnitPrice  DECIMAL(18,2) NOT NULL DEFAULT 0,
    LineTotal  DECIMAL(18,2) NOT NULL DEFAULT 0
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseOrderItems_OrderId')
CREATE INDEX IX_PurchaseOrderItems_OrderId ON Pur.PurchaseOrderItems(OrderId);
GO

PRINT N'سفارش خرید (Pur.PurchaseOrders / PurchaseOrderItems) با موفقیت ساخته شد.';
GO
