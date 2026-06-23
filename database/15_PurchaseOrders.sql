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

-- ── ارتقای idempotent برای پایگاه‌های قدیمی ──────────────────────────────────
-- باگ: جدول روی DBهای قدیمی پیش از افزودنِ این ستون‌ها ساخته شده و چون CREATE فقط
-- وقتی جدول نباشد اجرا می‌شد، ستون‌های جدید هرگز اضافه نمی‌شدند → خطای Invalid column name
-- در صفحهٔ «سفارش‌های خرید». این ALTERها ستون‌های گمشده را روی جدولِ موجود می‌افزایند.
IF COL_LENGTH('Pur.PurchaseOrders', 'StatusCode') IS NULL
    ALTER TABLE Pur.PurchaseOrders ADD StatusCode NVARCHAR(20) NOT NULL CONSTRAINT DF_PO_StatusCode DEFAULT N'پیش‌نویس';
IF COL_LENGTH('Pur.PurchaseOrders', 'Source') IS NULL
    ALTER TABLE Pur.PurchaseOrders ADD Source NVARCHAR(20) NOT NULL CONSTRAINT DF_PO_Source DEFAULT N'دستی';
IF COL_LENGTH('Pur.PurchaseOrders', 'Description') IS NULL
    ALTER TABLE Pur.PurchaseOrders ADD Description NVARCHAR(500) NULL;
IF COL_LENGTH('Pur.PurchaseOrders', 'Total') IS NULL
    ALTER TABLE Pur.PurchaseOrders ADD Total DECIMAL(18,2) NOT NULL CONSTRAINT DF_PO_Total DEFAULT 0;
IF COL_LENGTH('Pur.PurchaseOrders', 'CreatedAt') IS NULL
    ALTER TABLE Pur.PurchaseOrders ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PO_CreatedAt DEFAULT GETDATE();
IF COL_LENGTH('Pur.PurchaseOrders', 'UpdatedAt') IS NULL
    ALTER TABLE Pur.PurchaseOrders ADD UpdatedAt DATETIME2 NULL;
IF COL_LENGTH('Pur.PurchaseOrders', 'SupplierId') IS NULL
    ALTER TABLE Pur.PurchaseOrders ADD SupplierId INT NULL;
GO

-- روی DBهای با اسکیمای قدیمیِ ۰۲، ستون‌های FiscalYearId/SupplierId به‌صورتِ NOT NULL و بدونِ پیش‌فرض‌اند؛
-- چون موجودیتِ نو این‌ها را نمی‌فرستد، درجِ سفارشِ نو هم شکست می‌خورد. اگر این ستون‌ها هستند و NOT NULL،
-- nullable‌شان می‌کنیم تا درج با اسکیمای جدید کار کند (idempotent — فقط در صورتِ نیاز).
IF COL_LENGTH('Pur.PurchaseOrders', 'FiscalYearId') IS NOT NULL
   AND COLUMNPROPERTY(OBJECT_ID('Pur.PurchaseOrders'), 'FiscalYearId', 'AllowsNull') = 0
    ALTER TABLE Pur.PurchaseOrders ALTER COLUMN FiscalYearId INT NULL;
IF COL_LENGTH('Pur.PurchaseOrders', 'SupplierId') IS NOT NULL
   AND COLUMNPROPERTY(OBJECT_ID('Pur.PurchaseOrders'), 'SupplierId', 'AllowsNull') = 0
    ALTER TABLE Pur.PurchaseOrders ALTER COLUMN SupplierId INT NULL;
GO

PRINT N'سفارش خرید (Pur.PurchaseOrders / PurchaseOrderItems) با موفقیت ساخته/ارتقا یافت.';
GO
