-- 74_EnsureRestaurantSchema.sql — رفعِ باگِ واقعیِ کشف‌شده حینِ تستِ زندهٔ ماژولِ رستوران در وب:
-- 09_Restaurant.sql یک اسکریپتِ «پایه» (شمارهٔ ۲..۱۰) است که DatabaseMigrator فقط روی DBِ کاملاً
-- تازه (بدونِ Sec.Users/Cfg.Companies/Acc.Accounts) اجرا می‌کند. هر DBای که پیش از افزودنِ این
-- اسکریپت بوت‌استرپ شده (مثلِ DBِ dev همین نشست) اسکیمایِ Rst را هرگز نمی‌گیرد — `GET /api/restaurant/halls`
-- با «Invalid object name 'Rst.Halls'» (۵۰۰) شکست می‌خورد. رفع: عینِ همان محتوایِ idempotentِ ۰۹
-- به‌عنوانِ یک مهاجرتِ افزایشیِ ≥۱۱ کپی شد تا در استارت‌آپِ بعدی روی **هر DBِ موجودی** (نه فقط تازه)
-- خودکار اجرا شود؛ روی DBهایی که ۰۹ از قبل اجرا شده کاملاً بی‌اثر است (همان گاردهایِ IF OBJECT_ID).
IF SCHEMA_ID('Rst') IS NULL
    EXEC('CREATE SCHEMA Rst');
GO

IF OBJECT_ID('Rst.Halls', 'U') IS NULL
CREATE TABLE Rst.Halls (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId    INT NOT NULL,
    BranchId     INT NOT NULL,
    Name         NVARCHAR(100) NOT NULL,
    DisplayOrder INT NOT NULL DEFAULT 0,
    IsActive     BIT NOT NULL DEFAULT 1,
    CreatedAt    DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt    DATETIME2
);
GO

IF OBJECT_ID('Rst.DiningTables', 'U') IS NULL
CREATE TABLE Rst.DiningTables (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId      INT NOT NULL,
    BranchId       INT NOT NULL,
    HallId         INT NOT NULL REFERENCES Rst.Halls(Id),
    Name           NVARCHAR(50) NOT NULL,
    Capacity       INT NOT NULL DEFAULT 4,
    Status         INT NOT NULL DEFAULT 0,
    CurrentOrderId INT NULL,
    PositionX      INT NOT NULL DEFAULT 0,
    PositionY      INT NOT NULL DEFAULT 0,
    IsActive       BIT NOT NULL DEFAULT 1,
    CreatedAt      DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt      DATETIME2
);
GO

IF OBJECT_ID('Rst.RestaurantOrders', 'U') IS NULL
CREATE TABLE Rst.RestaurantOrders (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId      INT NOT NULL,
    BranchId       INT NOT NULL,
    OrderNumber    NVARCHAR(30) NOT NULL,
    OrderType      INT NOT NULL DEFAULT 0,
    Status         INT NOT NULL DEFAULT 0,
    TableId        INT NULL,
    GuestCount     INT NOT NULL DEFAULT 1,
    WaiterId       INT NULL,
    CustomerId     INT NULL,
    OpenedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    SettledAt      DATETIME2 NULL,
    SubTotal       DECIMAL(18,2) NOT NULL DEFAULT 0,
    Discount       DECIMAL(18,2) NOT NULL DEFAULT 0,
    ServiceCharge  DECIMAL(18,2) NOT NULL DEFAULT 0,
    Tax            DECIMAL(18,2) NOT NULL DEFAULT 0,
    Tip            DECIMAL(18,2) NOT NULL DEFAULT 0,
    GrandTotal     DECIMAL(18,2) NOT NULL DEFAULT 0,
    PaidAmount     DECIMAL(18,2) NOT NULL DEFAULT 0,
    Description    NVARCHAR(500) NULL,
    SalesInvoiceId INT NULL,
    CreatedAt      DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt      DATETIME2
);
GO

IF OBJECT_ID('Rst.RestaurantOrderItems', 'U') IS NULL
CREATE TABLE Rst.RestaurantOrderItems (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL,
    OrderId         INT NOT NULL REFERENCES Rst.RestaurantOrders(Id),
    ProductId       INT NOT NULL,
    ProductName     NVARCHAR(200) NOT NULL,
    Quantity        DECIMAL(18,3) NOT NULL DEFAULT 1,
    UnitPrice       DECIMAL(18,2) NOT NULL DEFAULT 0,
    DiscountAmount  DECIMAL(18,2) NOT NULL DEFAULT 0,
    LineTotal       DECIMAL(18,2) NOT NULL DEFAULT 0,
    Status          INT NOT NULL DEFAULT 0,
    Notes           NVARCHAR(300) NULL,
    KitchenTicketId INT NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME2
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RstOrderItems_OrderId')
    CREATE INDEX IX_RstOrderItems_OrderId ON Rst.RestaurantOrderItems(OrderId);
GO

IF OBJECT_ID('Rst.KitchenTickets', 'U') IS NULL
CREATE TABLE Rst.KitchenTickets (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId    INT NOT NULL,
    BranchId     INT NOT NULL,
    OrderId      INT NOT NULL,
    TicketNumber NVARCHAR(30) NOT NULL,
    TableName    NVARCHAR(50) NULL,
    Status       INT NOT NULL DEFAULT 0,
    ReadyAt      DATETIME2 NULL,
    CreatedAt    DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt    DATETIME2
);
GO

-- دادهٔ اولیه: یک سالنِ پیش‌فرض با ۶ میز، فقط اگر هیچ سالنی نبود (شرکتِ ۱، هم‌الگو با ۰۹).
IF NOT EXISTS (SELECT 1 FROM Rst.Halls)
BEGIN
    INSERT INTO Rst.Halls (CompanyId, BranchId, Name, DisplayOrder) VALUES (1, 1, N'سالن اصلی', 1);
    DECLARE @hall INT = SCOPE_IDENTITY();
    DECLARE @i INT = 1;
    WHILE @i <= 6
    BEGIN
        INSERT INTO Rst.DiningTables (CompanyId, BranchId, HallId, Name, Capacity, PositionX, PositionY)
        VALUES (1, 1, @hall, CONCAT(N'میز ', @i), 4, ((@i-1) % 3) * 120, ((@i-1) / 3) * 120);
        SET @i += 1;
    END
END
GO
