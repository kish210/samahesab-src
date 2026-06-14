-- =============================================================================
-- SAMA HESAB ERP - جداول پایگاه داده
-- =============================================================================
USE SamaHesab;
GO

-- =============================================================================
-- SCHEMA: Cfg (Configuration)
-- =============================================================================

CREATE TABLE Cfg.Companies (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Code            NVARCHAR(20) NOT NULL UNIQUE,
    Name            NVARCHAR(200) NOT NULL,
    NameEn          NVARCHAR(200),
    NationalId      NVARCHAR(11),
    EconomicCode    NVARCHAR(12),
    RegisterNumber  NVARCHAR(20),
    Address         NVARCHAR(500),
    Phone           NVARCHAR(20),
    Fax             NVARCHAR(20),
    Email           NVARCHAR(100),
    Website         NVARCHAR(200),
    Logo            VARBINARY(MAX),
    FiscalYearStart NVARCHAR(10) NOT NULL DEFAULT '1403/01/01',
    FiscalYearEnd   NVARCHAR(10) NOT NULL DEFAULT '1403/12/29',
    Currency        NVARCHAR(10) NOT NULL DEFAULT 'ریال',
    IsActive        BIT NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME2
);

CREATE TABLE Cfg.Branches (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL REFERENCES Cfg.Companies(Id),
    Code        NVARCHAR(20) NOT NULL,
    Name        NVARCHAR(200) NOT NULL,
    Address     NVARCHAR(500),
    Phone       NVARCHAR(20),
    ManagerName NVARCHAR(100),
    IsHQ        BIT NOT NULL DEFAULT 0,
    IsActive    BIT NOT NULL DEFAULT 1,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt   DATETIME2,
    UNIQUE(CompanyId, Code)
);

CREATE TABLE Cfg.FiscalYears (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL REFERENCES Cfg.Companies(Id),
    Title       NVARCHAR(50) NOT NULL,
    StartDate   NVARCHAR(10) NOT NULL,
    EndDate     NVARCHAR(10) NOT NULL,
    IsClosed    BIT NOT NULL DEFAULT 0,
    IsActive    BIT NOT NULL DEFAULT 1,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETDATE()
);

CREATE TABLE Cfg.Currencies (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Code            NVARCHAR(10) NOT NULL UNIQUE,
    Name            NVARCHAR(50) NOT NULL,
    Symbol          NVARCHAR(5),
    ExchangeRate    DECIMAL(18,4) NOT NULL DEFAULT 1,
    IsBase          BIT NOT NULL DEFAULT 0,
    IsActive        BIT NOT NULL DEFAULT 1,
    UpdatedAt       DATETIME2
);

CREATE TABLE Cfg.Units (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Code        NVARCHAR(10) NOT NULL UNIQUE,
    Name        NVARCHAR(50) NOT NULL,
    Abbreviation NVARCHAR(10),
    IsActive    BIT NOT NULL DEFAULT 1
);

CREATE TABLE Cfg.NumberingTemplates (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL REFERENCES Cfg.Companies(Id),
    ModuleCode  NVARCHAR(20) NOT NULL,
    DocType     NVARCHAR(50) NOT NULL,
    Prefix      NVARCHAR(10),
    Suffix      NVARCHAR(10),
    StartNumber INT NOT NULL DEFAULT 1,
    CurrentNumber INT NOT NULL DEFAULT 0,
    Padding     INT NOT NULL DEFAULT 6,
    IsAutomatic BIT NOT NULL DEFAULT 1,
    UNIQUE(CompanyId, ModuleCode, DocType)
);

-- =============================================================================
-- SCHEMA: Sec (Security)
-- =============================================================================

CREATE TABLE Sec.Roles (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Code        NVARCHAR(50) NOT NULL UNIQUE,
    Name        NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500),
    IsSystem    BIT NOT NULL DEFAULT 0,
    IsActive    BIT NOT NULL DEFAULT 1,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETDATE()
);

CREATE TABLE Sec.Permissions (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    ModuleCode  NVARCHAR(50) NOT NULL,
    FeatureCode NVARCHAR(100) NOT NULL,
    Name        NVARCHAR(200) NOT NULL,
    UNIQUE(ModuleCode, FeatureCode)
);

CREATE TABLE Sec.RolePermissions (
    RoleId          INT NOT NULL REFERENCES Sec.Roles(Id),
    PermissionId    INT NOT NULL REFERENCES Sec.Permissions(Id),
    CanCreate       BIT NOT NULL DEFAULT 0,
    CanRead         BIT NOT NULL DEFAULT 0,
    CanUpdate       BIT NOT NULL DEFAULT 0,
    CanDelete       BIT NOT NULL DEFAULT 0,
    CanPrint        BIT NOT NULL DEFAULT 0,
    CanExport       BIT NOT NULL DEFAULT 0,
    PRIMARY KEY (RoleId, PermissionId)
);

CREATE TABLE Sec.Users (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    BranchId        INT REFERENCES Cfg.Branches(Id),
    Username        NVARCHAR(50) NOT NULL,
    PasswordHash    NVARCHAR(256) NOT NULL,
    PasswordSalt    NVARCHAR(100) NOT NULL,
    FullName        NVARCHAR(100) NOT NULL,
    Email           NVARCHAR(100),
    Mobile          NVARCHAR(15),
    Avatar          VARBINARY(MAX),
    IsActive        BIT NOT NULL DEFAULT 1,
    IsLocked        BIT NOT NULL DEFAULT 0,
    LockoutEnd      DATETIME2,
    FailedAttempts  INT NOT NULL DEFAULT 0,
    LastLoginAt     DATETIME2,
    MustChangePass  BIT NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME2,
    UNIQUE(CompanyId, Username)
);

CREATE TABLE Sec.UserRoles (
    UserId  INT NOT NULL REFERENCES Sec.Users(Id),
    RoleId  INT NOT NULL REFERENCES Sec.Roles(Id),
    PRIMARY KEY (UserId, RoleId)
);

CREATE TABLE Sec.AuditLogs (
    Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId      INT REFERENCES Sec.Users(Id),
    Username    NVARCHAR(50),
    Action      NVARCHAR(50) NOT NULL,
    TableName   NVARCHAR(100),
    RecordId    NVARCHAR(50),
    OldValues   NVARCHAR(MAX),
    NewValues   NVARCHAR(MAX),
    IpAddress   NVARCHAR(50),
    UserAgent   NVARCHAR(200),
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETDATE()
);

CREATE TABLE Sec.UserSessions (
    Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId      INT NOT NULL REFERENCES Sec.Users(Id),
    Token       NVARCHAR(500) NOT NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETDATE(),
    ExpiresAt   DATETIME2 NOT NULL,
    IsRevoked   BIT NOT NULL DEFAULT 0
);

-- =============================================================================
-- SCHEMA: Acc (Accounting)
-- =============================================================================

CREATE TABLE Acc.AccountGroups (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Code        NVARCHAR(1) NOT NULL,
    Name        NVARCHAR(100) NOT NULL,
    Nature      NVARCHAR(10) NOT NULL, -- بدهکار/بستانکار
    IsActive    BIT NOT NULL DEFAULT 1,
    UNIQUE(Code)
);

CREATE TABLE Acc.Accounts (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    Code            NVARCHAR(20) NOT NULL,
    Name            NVARCHAR(200) NOT NULL,
    Level           TINYINT NOT NULL, -- 1=گروه, 2=کل, 3=معین, 4=تفصیلی
    ParentId        INT REFERENCES Acc.Accounts(Id),
    Nature          NVARCHAR(10) NOT NULL DEFAULT 'بدهکار', -- بدهکار/بستانکار
    AccountType     NVARCHAR(50), -- دارایی/بدهی/سرمایه/درآمد/هزینه
    IsLeaf          BIT NOT NULL DEFAULT 0,
    IsSystem        BIT NOT NULL DEFAULT 0,
    CurrencyId      INT REFERENCES Cfg.Currencies(Id),
    Description     NVARCHAR(500),
    IsActive        BIT NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME2,
    UNIQUE(CompanyId, Code)
);

CREATE TABLE Acc.VoucherTypes (
    Id      INT IDENTITY(1,1) PRIMARY KEY,
    Code    NVARCHAR(10) NOT NULL UNIQUE,
    Name    NVARCHAR(50) NOT NULL,
    Prefix  NVARCHAR(5)
);

CREATE TABLE Acc.Vouchers (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    BranchId        INT NOT NULL REFERENCES Cfg.Branches(Id),
    FiscalYearId    INT NOT NULL REFERENCES Cfg.FiscalYears(Id),
    VoucherNumber   NVARCHAR(20) NOT NULL,
    VoucherDate     NVARCHAR(10) NOT NULL,
    VoucherTypeId   INT NOT NULL REFERENCES Acc.VoucherTypes(Id),
    Status          TINYINT NOT NULL DEFAULT 1, -- 1=پیش‌نویس, 2=قطعی, 3=دائمی
    Description     NVARCHAR(1000),
    Reference       NVARCHAR(100),
    TotalDebit      DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalCredit     DECIMAL(18,2) NOT NULL DEFAULT 0,
    CurrencyId      INT REFERENCES Cfg.Currencies(Id),
    ExchangeRate    DECIMAL(18,4) NOT NULL DEFAULT 1,
    CreatedByUserId INT REFERENCES Sec.Users(Id),
    ApprovedByUserId INT REFERENCES Sec.Users(Id),
    ApprovedAt      DATETIME2,
    ReversedFromId  INT REFERENCES Acc.Vouchers(Id),
    IsReversed      BIT NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME2,
    UNIQUE(CompanyId, FiscalYearId, VoucherNumber)
);

CREATE TABLE Acc.VoucherItems (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    VoucherId       INT NOT NULL REFERENCES Acc.Vouchers(Id) ON DELETE CASCADE,
    RowNumber       INT NOT NULL,
    AccountId       INT NOT NULL REFERENCES Acc.Accounts(Id),
    Description     NVARCHAR(500),
    Debit           DECIMAL(18,2) NOT NULL DEFAULT 0,
    Credit          DECIMAL(18,2) NOT NULL DEFAULT 0,
    CurrencyId      INT REFERENCES Cfg.Currencies(Id),
    ForeignDebit    DECIMAL(18,4),
    ForeignCredit   DECIMAL(18,4),
    ExchangeRate    DECIMAL(18,4) NOT NULL DEFAULT 1,
    CostCenterId    INT,
    ProjectId       INT,
    CheckId         INT,
    Reference       NVARCHAR(100)
);

CREATE TABLE Acc.CostCenters (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL REFERENCES Cfg.Companies(Id),
    Code        NVARCHAR(20) NOT NULL,
    Name        NVARCHAR(100) NOT NULL,
    ParentId    INT REFERENCES Acc.CostCenters(Id),
    IsActive    BIT NOT NULL DEFAULT 1,
    UNIQUE(CompanyId, Code)
);

CREATE TABLE Acc.Projects (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL REFERENCES Cfg.Companies(Id),
    Code        NVARCHAR(20) NOT NULL,
    Name        NVARCHAR(100) NOT NULL,
    StartDate   NVARCHAR(10),
    EndDate     NVARCHAR(10),
    Budget      DECIMAL(18,2),
    IsActive    BIT NOT NULL DEFAULT 1,
    UNIQUE(CompanyId, Code)
);

-- Bank & Cash
CREATE TABLE Acc.BankAccounts (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    BranchId        INT REFERENCES Cfg.Branches(Id),
    AccountId       INT NOT NULL REFERENCES Acc.Accounts(Id),
    BankName        NVARCHAR(100) NOT NULL,
    AccountNumber   NVARCHAR(30) NOT NULL,
    ShebaNumber     NVARCHAR(30),
    CardNumber      NVARCHAR(20),
    BranchName      NVARCHAR(100),
    BranchCode      NVARCHAR(10),
    OpeningBalance  DECIMAL(18,2) NOT NULL DEFAULT 0,
    IsActive        BIT NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE()
);

CREATE TABLE Acc.CashRegisters (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL REFERENCES Cfg.Companies(Id),
    BranchId    INT NOT NULL REFERENCES Cfg.Branches(Id),
    AccountId   INT NOT NULL REFERENCES Acc.Accounts(Id),
    Code        NVARCHAR(10) NOT NULL,
    Name        NVARCHAR(100) NOT NULL,
    OpenBalance DECIMAL(18,2) NOT NULL DEFAULT 0,
    IsActive    BIT NOT NULL DEFAULT 1,
    UNIQUE(CompanyId, Code)
);

-- Cheques
CREATE TABLE Acc.Cheques (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    BranchId        INT REFERENCES Cfg.Branches(Id),
    ChequeType      NVARCHAR(10) NOT NULL, -- دریافتی/پرداختی
    ChequeNumber    NVARCHAR(20) NOT NULL,
    BankName        NVARCHAR(100) NOT NULL,
    AccountNumber   NVARCHAR(30),
    BranchName      NVARCHAR(100),
    Amount          DECIMAL(18,2) NOT NULL,
    DueDate         NVARCHAR(10) NOT NULL,
    IssueDate       NVARCHAR(10),
    IssuedBy        NVARCHAR(100),
    Description     NVARCHAR(500),
    Status          NVARCHAR(20) NOT NULL DEFAULT 'در جریان',
    -- وضعیت: در جریان/وصول شده/برگشت خورده/واگذار شده/ابطال شده
    AccountId       INT REFERENCES Acc.Accounts(Id),
    PartyId         INT, -- مشتری یا تأمین‌کننده
    PartyType       NVARCHAR(10), -- مشتری/تأمین‌کننده
    ReceiveVoucherId INT REFERENCES Acc.Vouchers(Id),
    PayVoucherId     INT REFERENCES Acc.Vouchers(Id),
    ClearedVoucherId INT REFERENCES Acc.Vouchers(Id),
    ReturnedAt      DATETIME2,
    ReturnReason    NVARCHAR(200),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME2
);

CREATE TABLE Acc.BankReconciliations (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    BankAccountId   INT NOT NULL REFERENCES Acc.BankAccounts(Id),
    PeriodDate      NVARCHAR(10) NOT NULL,
    BankBalance     DECIMAL(18,2) NOT NULL,
    BookBalance     DECIMAL(18,2) NOT NULL,
    Difference      DECIMAL(18,2) NOT NULL,
    IsCompleted     BIT NOT NULL DEFAULT 0,
    CompletedAt     DATETIME2,
    Notes           NVARCHAR(1000),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- =============================================================================
-- SCHEMA: Inv (Inventory)
-- =============================================================================

CREATE TABLE Inv.ProductGroups (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL REFERENCES Cfg.Companies(Id),
    Code        NVARCHAR(20) NOT NULL,
    Name        NVARCHAR(200) NOT NULL,
    ParentId    INT REFERENCES Inv.ProductGroups(Id),
    AccountId   INT REFERENCES Acc.Accounts(Id), -- حساب موجودی کالا
    IsActive    BIT NOT NULL DEFAULT 1,
    UNIQUE(CompanyId, Code)
);

CREATE TABLE Inv.Brands (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL REFERENCES Cfg.Companies(Id),
    Name        NVARCHAR(100) NOT NULL,
    IsActive    BIT NOT NULL DEFAULT 1
);

CREATE TABLE Inv.Products (
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId           INT NOT NULL REFERENCES Cfg.Companies(Id),
    Code                NVARCHAR(30) NOT NULL,
    Barcode             NVARCHAR(50),
    Barcode2            NVARCHAR(50),
    Name                NVARCHAR(300) NOT NULL,
    NameEn              NVARCHAR(300),
    GroupId             INT REFERENCES Inv.ProductGroups(Id),
    BrandId             INT REFERENCES Inv.Brands(Id),
    UnitId              INT NOT NULL REFERENCES Cfg.Units(Id),
    Unit2Id             INT REFERENCES Cfg.Units(Id),
    Unit2Factor         DECIMAL(18,4),
    ProductType         NVARCHAR(20) NOT NULL DEFAULT 'کالا', -- کالا/خدمات/مجموعه
    PurchasePrice       DECIMAL(18,2) NOT NULL DEFAULT 0,
    SalePrice           DECIMAL(18,2) NOT NULL DEFAULT 0,
    WholesalePrice      DECIMAL(18,2) NOT NULL DEFAULT 0,
    ConsumerPrice       DECIMAL(18,2) NOT NULL DEFAULT 0,
    MinStock            DECIMAL(18,4) NOT NULL DEFAULT 0,
    MaxStock            DECIMAL(18,4),
    ReorderPoint        DECIMAL(18,4),
    HasSerial           BIT NOT NULL DEFAULT 0,
    HasBatch            BIT NOT NULL DEFAULT 0,
    HasExpiry           BIT NOT NULL DEFAULT 0,
    ValuationMethod     NVARCHAR(20) NOT NULL DEFAULT 'میانگین', -- FIFO/میانگین
    TaxRate             DECIMAL(5,2) NOT NULL DEFAULT 0,
    Image               VARBINARY(MAX),
    Description         NVARCHAR(2000),
    IsActive            BIT NOT NULL DEFAULT 1,
    CreatedAt           DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt           DATETIME2,
    UNIQUE(CompanyId, Code)
);

CREATE TABLE Inv.ProductPriceLevels (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    ProductId   INT NOT NULL REFERENCES Inv.Products(Id) ON DELETE CASCADE,
    LevelCode   NVARCHAR(20) NOT NULL, -- خرده/عمده/ویژه
    Price       DECIMAL(18,2) NOT NULL,
    MinQty      DECIMAL(18,4),
    ValidFrom   NVARCHAR(10),
    ValidTo     NVARCHAR(10),
    IsActive    BIT NOT NULL DEFAULT 1
);

CREATE TABLE Inv.Warehouses (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL REFERENCES Cfg.Companies(Id),
    BranchId    INT REFERENCES Cfg.Branches(Id),
    Code        NVARCHAR(20) NOT NULL,
    Name        NVARCHAR(200) NOT NULL,
    Address     NVARCHAR(500),
    ManagerName NVARCHAR(100),
    IsDefault   BIT NOT NULL DEFAULT 0,
    IsActive    BIT NOT NULL DEFAULT 1,
    UNIQUE(CompanyId, Code)
);

CREATE TABLE Inv.Batches (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    ProductId       INT NOT NULL REFERENCES Inv.Products(Id),
    BatchNumber     NVARCHAR(50) NOT NULL,
    ProductionDate  NVARCHAR(10),
    ExpiryDate      NVARCHAR(10),
    Quantity        DECIMAL(18,4) NOT NULL DEFAULT 0,
    PurchasePrice   DECIMAL(18,2),
    Notes           NVARCHAR(500),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UNIQUE(ProductId, BatchNumber)
);

CREATE TABLE Inv.Serials (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    ProductId   INT NOT NULL REFERENCES Inv.Products(Id),
    WarehouseId INT REFERENCES Inv.Warehouses(Id),
    SerialNumber NVARCHAR(100) NOT NULL,
    Status      NVARCHAR(20) NOT NULL DEFAULT 'موجود', -- موجود/فروخته شده/معیوب
    PurchasePrice DECIMAL(18,2),
    PurchaseDate NVARCHAR(10),
    SaleDate    NVARCHAR(10),
    Notes       NVARCHAR(500),
    UNIQUE(ProductId, SerialNumber)
);

-- پله‌های تخفیفِ مقداریِ کالا (U6): «مقدار ≥ MinQty → DiscountPercent٪»
CREATE TABLE Inv.ProductDiscountTiers (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    ProductId       INT NOT NULL REFERENCES Inv.Products(Id) ON DELETE CASCADE,
    MinQty          DECIMAL(18,3) NOT NULL,
    DiscountPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME2
);

CREATE TABLE Inv.StockItems (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    ProductId       INT NOT NULL REFERENCES Inv.Products(Id),
    WarehouseId     INT NOT NULL REFERENCES Inv.Warehouses(Id),
    Quantity        DECIMAL(18,4) NOT NULL DEFAULT 0,
    AverageCost     DECIMAL(18,4) NOT NULL DEFAULT 0,
    LastCost        DECIMAL(18,4),
    LastUpdated     DATETIME2 NOT NULL DEFAULT GETDATE(),
    UNIQUE(ProductId, WarehouseId)
);

CREATE TABLE Inv.StockTransactionTypes (
    Id      INT IDENTITY(1,1) PRIMARY KEY,
    Code    NVARCHAR(20) NOT NULL UNIQUE,
    Name    NVARCHAR(100) NOT NULL,
    Effect  SMALLINT NOT NULL -- 1=ورود, -1=خروج, 0=خنثی
);

CREATE TABLE Inv.StockTransactions (
    Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    BranchId        INT REFERENCES Cfg.Branches(Id),
    TransactionType NVARCHAR(20) NOT NULL,
    DocumentNumber  NVARCHAR(20),
    DocumentDate    NVARCHAR(10) NOT NULL,
    ProductId       INT NOT NULL REFERENCES Inv.Products(Id),
    WarehouseId     INT NOT NULL REFERENCES Inv.Warehouses(Id),
    BatchId         INT REFERENCES Inv.Batches(Id),
    SerialId        INT REFERENCES Inv.Serials(Id),
    Quantity        DECIMAL(18,4) NOT NULL,
    UnitCost        DECIMAL(18,4) NOT NULL DEFAULT 0,
    TotalCost       DECIMAL(18,4) NOT NULL DEFAULT 0,
    BalanceQty      DECIMAL(18,4) NOT NULL DEFAULT 0,
    BalanceCost     DECIMAL(18,4) NOT NULL DEFAULT 0,
    RelatedDocType  NVARCHAR(50),
    RelatedDocId    INT,
    Notes           NVARCHAR(500),
    CreatedByUserId INT REFERENCES Sec.Users(Id),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- =============================================================================
-- SCHEMA: Crm (CRM)
-- =============================================================================

CREATE TABLE Crm.CustomerGroups (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL REFERENCES Cfg.Companies(Id),
    Code        NVARCHAR(20) NOT NULL,
    Name        NVARCHAR(100) NOT NULL,
    Discount    DECIMAL(5,2) NOT NULL DEFAULT 0,
    IsActive    BIT NOT NULL DEFAULT 1,
    UNIQUE(CompanyId, Code)
);

CREATE TABLE Crm.Customers (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    Code            NVARCHAR(20) NOT NULL,
    CustomerType    NVARCHAR(10) NOT NULL DEFAULT 'حقیقی', -- حقیقی/حقوقی
    FirstName       NVARCHAR(100),
    LastName        NVARCHAR(100),
    CompanyName     NVARCHAR(200),
    NationalCode    NVARCHAR(11),
    EconomicCode    NVARCHAR(12),
    RegisterNumber  NVARCHAR(20),
    GroupId         INT REFERENCES Crm.CustomerGroups(Id),
    AccountId       INT REFERENCES Acc.Accounts(Id),
    Phone           NVARCHAR(20),
    Mobile          NVARCHAR(15),
    Phone2          NVARCHAR(20),
    Email           NVARCHAR(100),
    Province        NVARCHAR(50),
    City            NVARCHAR(50),
    Address         NVARCHAR(500),
    PostalCode      NVARCHAR(10),
    BirthDate       NVARCHAR(10),
    CreditLimit     DECIMAL(18,2) NOT NULL DEFAULT 0,
    CreditDays      INT NOT NULL DEFAULT 0,
    PriceLevel      NVARCHAR(20) NOT NULL DEFAULT 'خرده',
    Discount        DECIMAL(5,2) NOT NULL DEFAULT 0,
    LoyaltyPoints   INT NOT NULL DEFAULT 0,
    Balance         DECIMAL(18,2) NOT NULL DEFAULT 0,
    IsActive        BIT NOT NULL DEFAULT 1,
    Notes           NVARCHAR(2000),
    ContactPerson   NVARCHAR(100),   -- شخصِ رابط
    Visitor         NVARCHAR(100),   -- ویزیتور/بازاریاب
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME2,
    UNIQUE(CompanyId, Code)
);
GO

-- اسنادِ ضمیمهٔ مشتری (کارِ ۹) — فایل در سیستم‌فایلِ محلی، متادیتا اینجا
CREATE TABLE Crm.CustomerAttachments (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    CustomerId      INT NOT NULL REFERENCES Crm.Customers(Id) ON DELETE CASCADE,
    FileName        NVARCHAR(260) NOT NULL,
    StoredPath      NVARCHAR(500) NOT NULL,
    ContentType     NVARCHAR(100),
    FileSize        BIGINT NOT NULL DEFAULT 0,
    UploadedAt      NVARCHAR(10) NOT NULL,
    Description     NVARCHAR(500),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME2
);

CREATE TABLE Crm.CustomerContacts (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId  INT NOT NULL REFERENCES Crm.Customers(Id) ON DELETE CASCADE,
    Name        NVARCHAR(100) NOT NULL,
    Role        NVARCHAR(50),
    Mobile      NVARCHAR(15),
    Email       NVARCHAR(100),
    IsDefault   BIT NOT NULL DEFAULT 0
);

CREATE TABLE Crm.LoyaltyTransactions (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId  INT NOT NULL REFERENCES Crm.Customers(Id),
    Points      INT NOT NULL,
    Type        NVARCHAR(20) NOT NULL, -- کسب/استفاده
    Description NVARCHAR(200),
    RelatedDoc  NVARCHAR(100),
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- Suppliers
CREATE TABLE Crm.Suppliers (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    Code            NVARCHAR(20) NOT NULL,
    SupplierType    NVARCHAR(10) NOT NULL DEFAULT 'حقیقی',
    FirstName       NVARCHAR(100),
    LastName        NVARCHAR(100),
    CompanyName     NVARCHAR(200),
    NationalCode    NVARCHAR(11),
    EconomicCode    NVARCHAR(12),
    AccountId       INT REFERENCES Acc.Accounts(Id),
    Phone           NVARCHAR(20),
    Mobile          NVARCHAR(15),
    Email           NVARCHAR(100),
    Province        NVARCHAR(50),
    City            NVARCHAR(50),
    Address         NVARCHAR(500),
    PostalCode      NVARCHAR(10),
    CreditLimit     DECIMAL(18,2) NOT NULL DEFAULT 0,
    CreditDays      INT NOT NULL DEFAULT 0,
    Balance         DECIMAL(18,2) NOT NULL DEFAULT 0,
    IsActive        BIT NOT NULL DEFAULT 1,
    Notes           NVARCHAR(2000),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME2,
    UNIQUE(CompanyId, Code)
);

-- =============================================================================
-- SCHEMA: Sal (Sales)
-- =============================================================================

CREATE TABLE Sal.InvoiceStatuses (
    Id      INT IDENTITY(1,1) PRIMARY KEY,
    Code    NVARCHAR(20) NOT NULL UNIQUE,
    Name    NVARCHAR(50) NOT NULL
);

CREATE TABLE Sal.SalesInvoices (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    BranchId        INT NOT NULL REFERENCES Cfg.Branches(Id),
    FiscalYearId    INT NOT NULL REFERENCES Cfg.FiscalYears(Id),
    InvoiceNumber   NVARCHAR(20) NOT NULL,
    InvoiceDate     NVARCHAR(10) NOT NULL,
    InvoiceType     NVARCHAR(20) NOT NULL DEFAULT 'فروش', -- فروش/برگشت از فروش/پیش‌فاکتور/حواله
    CustomerId      INT NOT NULL REFERENCES Crm.Customers(Id),
    WarehouseId     INT NOT NULL REFERENCES Inv.Warehouses(Id),
    PriceLevel      NVARCHAR(20) NOT NULL DEFAULT 'خرده',
    SalesRepId      INT REFERENCES Sec.Users(Id),
    CurrencyId      INT REFERENCES Cfg.Currencies(Id),
    ExchangeRate    DECIMAL(18,4) NOT NULL DEFAULT 1,
    SubTotal        DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalDiscount   DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalTax        DECIMAL(18,2) NOT NULL DEFAULT 0,
    Shipping        DECIMAL(18,2) NOT NULL DEFAULT 0,
    OtherCosts      DECIMAL(18,2) NOT NULL DEFAULT 0,
    GrandTotal      DECIMAL(18,2) NOT NULL DEFAULT 0,
    PaidAmount      DECIMAL(18,2) NOT NULL DEFAULT 0,
    RemainAmount    DECIMAL(18,2) NOT NULL DEFAULT 0,
    StatusCode      NVARCHAR(20) NOT NULL DEFAULT 'پیش‌نویس',
    DueDate         NVARCHAR(10),
    Description     NVARCHAR(1000),
    PrintCount      INT NOT NULL DEFAULT 0,
    VoucherId       INT REFERENCES Acc.Vouchers(Id),
    ReturnedFromId  INT REFERENCES Sal.SalesInvoices(Id),
    CreatedByUserId INT REFERENCES Sec.Users(Id),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME2,
    UNIQUE(CompanyId, FiscalYearId, InvoiceNumber, InvoiceType)
);

CREATE TABLE Sal.SalesInvoiceItems (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId       INT NOT NULL REFERENCES Sal.SalesInvoices(Id) ON DELETE CASCADE,
    RowNumber       INT NOT NULL,
    ProductId       INT NOT NULL REFERENCES Inv.Products(Id),
    BatchId         INT REFERENCES Inv.Batches(Id),
    SerialId        INT REFERENCES Inv.Serials(Id),
    Description     NVARCHAR(300),
    Quantity        DECIMAL(18,4) NOT NULL,
    UnitPrice       DECIMAL(18,2) NOT NULL,
    DiscountPct     DECIMAL(5,2) NOT NULL DEFAULT 0,
    DiscountAmount  DECIMAL(18,2) NOT NULL DEFAULT 0,
    TaxPct          DECIMAL(5,2) NOT NULL DEFAULT 0,
    TaxAmount       DECIMAL(18,2) NOT NULL DEFAULT 0,
    NetAmount       DECIMAL(18,2) NOT NULL,
    CostPrice       DECIMAL(18,4),
    Profit          DECIMAL(18,2)
);

CREATE TABLE Sal.SalesPayments (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId       INT NOT NULL REFERENCES Sal.SalesInvoices(Id),
    PaymentDate     NVARCHAR(10) NOT NULL,
    PaymentType     NVARCHAR(20) NOT NULL, -- نقدی/کارتخوان/چک/حواله/اقساط
    Amount          DECIMAL(18,2) NOT NULL,
    BankAccountId   INT REFERENCES Acc.BankAccounts(Id),
    ChequeId        INT REFERENCES Acc.Cheques(Id),
    ReferenceNumber NVARCHAR(50),
    Description     NVARCHAR(200),
    VoucherId       INT REFERENCES Acc.Vouchers(Id),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE()
);

CREATE TABLE Sal.Quotations (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    BranchId        INT NOT NULL REFERENCES Cfg.Branches(Id),
    QuoteNumber     NVARCHAR(20) NOT NULL,
    QuoteDate       NVARCHAR(10) NOT NULL,
    ValidUntil      NVARCHAR(10),
    CustomerId      INT NOT NULL REFERENCES Crm.Customers(Id),
    GrandTotal      DECIMAL(18,2) NOT NULL DEFAULT 0,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'در انتظار',
    ConvertedToInvoiceId INT REFERENCES Sal.SalesInvoices(Id),
    Notes           NVARCHAR(1000),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- =============================================================================
-- SCHEMA: Pur (Purchase)
-- =============================================================================

CREATE TABLE Pur.PurchaseOrders (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    BranchId        INT NOT NULL REFERENCES Cfg.Branches(Id),
    FiscalYearId    INT NOT NULL REFERENCES Cfg.FiscalYears(Id),
    OrderNumber     NVARCHAR(20) NOT NULL,
    OrderDate       NVARCHAR(10) NOT NULL,
    SupplierId      INT NOT NULL REFERENCES Crm.Suppliers(Id),
    ExpectedDate    NVARCHAR(10),
    GrandTotal      DECIMAL(18,2) NOT NULL DEFAULT 0,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'پیش‌نویس',
    Notes           NVARCHAR(1000),
    CreatedByUserId INT REFERENCES Sec.Users(Id),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UNIQUE(CompanyId, FiscalYearId, OrderNumber)
);

CREATE TABLE Pur.PurchaseOrderItems (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    OrderId         INT NOT NULL REFERENCES Pur.PurchaseOrders(Id) ON DELETE CASCADE,
    ProductId       INT NOT NULL REFERENCES Inv.Products(Id),
    Quantity        DECIMAL(18,4) NOT NULL,
    UnitPrice       DECIMAL(18,2) NOT NULL,
    DiscountPct     DECIMAL(5,2) NOT NULL DEFAULT 0,
    TaxPct          DECIMAL(5,2) NOT NULL DEFAULT 0,
    NetAmount       DECIMAL(18,2) NOT NULL,
    ReceivedQty     DECIMAL(18,4) NOT NULL DEFAULT 0
);

CREATE TABLE Pur.PurchaseInvoices (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    BranchId        INT NOT NULL REFERENCES Cfg.Branches(Id),
    FiscalYearId    INT NOT NULL REFERENCES Cfg.FiscalYears(Id),
    InvoiceNumber   NVARCHAR(20) NOT NULL,
    InvoiceDate     NVARCHAR(10) NOT NULL,
    InvoiceType     NVARCHAR(20) NOT NULL DEFAULT 'خرید',
    SupplierId      INT NOT NULL REFERENCES Crm.Suppliers(Id),
    WarehouseId     INT NOT NULL REFERENCES Inv.Warehouses(Id),
    OrderId         INT REFERENCES Pur.PurchaseOrders(Id),
    SubTotal        DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalDiscount   DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalTax        DECIMAL(18,2) NOT NULL DEFAULT 0,
    Shipping        DECIMAL(18,2) NOT NULL DEFAULT 0,
    OtherCosts      DECIMAL(18,2) NOT NULL DEFAULT 0,
    GrandTotal      DECIMAL(18,2) NOT NULL DEFAULT 0,
    PaidAmount      DECIMAL(18,2) NOT NULL DEFAULT 0,
    RemainAmount    DECIMAL(18,2) NOT NULL DEFAULT 0,
    StatusCode      NVARCHAR(20) NOT NULL DEFAULT 'پیش‌نویس',
    DueDate         NVARCHAR(10),
    Description     NVARCHAR(1000),
    VoucherId       INT REFERENCES Acc.Vouchers(Id),
    ReturnedFromId  INT REFERENCES Pur.PurchaseInvoices(Id),
    CreatedByUserId INT REFERENCES Sec.Users(Id),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME2,
    UNIQUE(CompanyId, FiscalYearId, InvoiceNumber, InvoiceType)
);

CREATE TABLE Pur.PurchaseInvoiceItems (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId       INT NOT NULL REFERENCES Pur.PurchaseInvoices(Id) ON DELETE CASCADE,
    RowNumber       INT NOT NULL,
    ProductId       INT NOT NULL REFERENCES Inv.Products(Id),
    BatchId         INT REFERENCES Inv.Batches(Id),
    SerialId        INT REFERENCES Inv.Serials(Id),
    Description     NVARCHAR(300),
    Quantity        DECIMAL(18,4) NOT NULL,
    UnitPrice       DECIMAL(18,2) NOT NULL,
    DiscountPct     DECIMAL(5,2) NOT NULL DEFAULT 0,
    DiscountAmount  DECIMAL(18,2) NOT NULL DEFAULT 0,
    TaxPct          DECIMAL(5,2) NOT NULL DEFAULT 0,
    TaxAmount       DECIMAL(18,2) NOT NULL DEFAULT 0,
    NetAmount       DECIMAL(18,2) NOT NULL,
    AdditionalCost  DECIMAL(18,2) NOT NULL DEFAULT 0,
    LandedCost      DECIMAL(18,2) NOT NULL DEFAULT 0
);

CREATE TABLE Pur.PurchasePayments (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId       INT NOT NULL REFERENCES Pur.PurchaseInvoices(Id),
    PaymentDate     NVARCHAR(10) NOT NULL,
    PaymentType     NVARCHAR(20) NOT NULL,
    Amount          DECIMAL(18,2) NOT NULL,
    BankAccountId   INT REFERENCES Acc.BankAccounts(Id),
    ChequeId        INT REFERENCES Acc.Cheques(Id),
    ReferenceNumber NVARCHAR(50),
    Description     NVARCHAR(200),
    VoucherId       INT REFERENCES Acc.Vouchers(Id),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- =============================================================================
-- SCHEMA: Pos (Point of Sale)
-- =============================================================================

CREATE TABLE Pos.PosDevices (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    BranchId        INT NOT NULL REFERENCES Cfg.Branches(Id),
    Code            NVARCHAR(20) NOT NULL,
    Name            NVARCHAR(100) NOT NULL,
    WarehouseId     INT NOT NULL REFERENCES Inv.Warehouses(Id),
    CashRegisterId  INT REFERENCES Acc.CashRegisters(Id),
    PrinterName     NVARCHAR(100),
    ReceiptFooter   NVARCHAR(500),
    IsActive        BIT NOT NULL DEFAULT 1,
    UNIQUE(CompanyId, Code)
);

CREATE TABLE Pos.PosSessions (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    DeviceId        INT NOT NULL REFERENCES Pos.PosDevices(Id),
    UserId          INT NOT NULL REFERENCES Sec.Users(Id),
    OpeningBalance  DECIMAL(18,2) NOT NULL DEFAULT 0,
    ClosingBalance  DECIMAL(18,2),
    ExpectedBalance DECIMAL(18,2),
    Difference      DECIMAL(18,2),
    OpenedAt        DATETIME2 NOT NULL DEFAULT GETDATE(),
    ClosedAt        DATETIME2,
    Status          NVARCHAR(10) NOT NULL DEFAULT 'باز'
);

CREATE TABLE Pos.PosTransactions (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    SessionId       INT NOT NULL REFERENCES Pos.PosSessions(Id),
    InvoiceId       INT REFERENCES Sal.SalesInvoices(Id),
    TransactionDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    CustomerId      INT REFERENCES Crm.Customers(Id),
    SubTotal        DECIMAL(18,2) NOT NULL DEFAULT 0,
    Discount        DECIMAL(18,2) NOT NULL DEFAULT 0,
    Tax             DECIMAL(18,2) NOT NULL DEFAULT 0,
    Total           DECIMAL(18,2) NOT NULL DEFAULT 0,
    CashPaid        DECIMAL(18,2) NOT NULL DEFAULT 0,
    CardPaid        DECIMAL(18,2) NOT NULL DEFAULT 0,
    ChangeDue       DECIMAL(18,2) NOT NULL DEFAULT 0,
    IsVoided        BIT NOT NULL DEFAULT 0,
    VoidReason      NVARCHAR(200),
    ReceiptNumber   NVARCHAR(20)
);

-- =============================================================================
-- SCHEMA: Hrm (Human Resources)
-- =============================================================================

CREATE TABLE Hrm.Departments (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL REFERENCES Cfg.Companies(Id),
    BranchId    INT REFERENCES Cfg.Branches(Id),
    Code        NVARCHAR(20) NOT NULL,
    Name        NVARCHAR(100) NOT NULL,
    ParentId    INT REFERENCES Hrm.Departments(Id),
    ManagerId   INT,
    IsActive    BIT NOT NULL DEFAULT 1,
    UNIQUE(CompanyId, Code)
);

CREATE TABLE Hrm.Positions (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL REFERENCES Cfg.Companies(Id),
    Code        NVARCHAR(20) NOT NULL,
    Name        NVARCHAR(100) NOT NULL,
    BaseSalary  DECIMAL(18,2),
    IsActive    BIT NOT NULL DEFAULT 1,
    UNIQUE(CompanyId, Code)
);

CREATE TABLE Hrm.Employees (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
    BranchId        INT REFERENCES Cfg.Branches(Id),
    Code            NVARCHAR(20) NOT NULL,
    NationalCode    NVARCHAR(11) NOT NULL,
    FirstName       NVARCHAR(100) NOT NULL,
    LastName        NVARCHAR(100) NOT NULL,
    FatherName      NVARCHAR(100),
    BirthDate       NVARCHAR(10),
    BirthPlace      NVARCHAR(100),
    Gender          NVARCHAR(10),
    MaritalStatus   NVARCHAR(10),
    MilitaryStatus  NVARCHAR(20),
    Education       NVARCHAR(20),
    Mobile          NVARCHAR(15),
    Phone           NVARCHAR(20),
    Email           NVARCHAR(100),
    Address         NVARCHAR(500),
    DepartmentId    INT REFERENCES Hrm.Departments(Id),
    PositionId      INT REFERENCES Hrm.Positions(Id),
    HireDate        NVARCHAR(10) NOT NULL,
    ContractType    NVARCHAR(20) NOT NULL DEFAULT 'دائم', -- دائم/موقت/پاره وقت/پیمانی
    BaseSalary      DECIMAL(18,2) NOT NULL DEFAULT 0,
    BankAccount     NVARCHAR(30),
    BankName        NVARCHAR(100),
    ShebaNumber     NVARCHAR(30),
    InsuranceNumber NVARCHAR(20),
    TaxCode         NVARCHAR(20),
    Photo           VARBINARY(MAX),
    IsActive        BIT NOT NULL DEFAULT 1,
    TerminationDate NVARCHAR(10),
    TerminationReason NVARCHAR(200),
    UserId          INT REFERENCES Sec.Users(Id),
    AccountId       INT REFERENCES Acc.Accounts(Id),
    Notes           NVARCHAR(2000),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME2,
    UNIQUE(CompanyId, Code),
    UNIQUE(CompanyId, NationalCode)
);

CREATE TABLE Hrm.AttendanceRecords (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId      INT NOT NULL REFERENCES Hrm.Employees(Id),
    WorkDate        NVARCHAR(10) NOT NULL,
    CheckIn         TIME,
    CheckOut        TIME,
    WorkHours       DECIMAL(5,2),
    OvertimeHours   DECIMAL(5,2),
    Status          NVARCHAR(20) NOT NULL DEFAULT 'حاضر', -- حاضر/غایب/مرخصی/تعطیل
    LeaveType       NVARCHAR(20),
    Notes           NVARCHAR(200),
    UNIQUE(EmployeeId, WorkDate)
);

CREATE TABLE Hrm.SalarySlips (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId      INT NOT NULL REFERENCES Hrm.Employees(Id),
    PeriodYear      NVARCHAR(4) NOT NULL,
    PeriodMonth     TINYINT NOT NULL,
    BaseSalary      DECIMAL(18,2) NOT NULL,
    OvertimePay     DECIMAL(18,2) NOT NULL DEFAULT 0,
    Allowances      DECIMAL(18,2) NOT NULL DEFAULT 0,
    Commission      DECIMAL(18,2) NOT NULL DEFAULT 0,
    Bonuses         DECIMAL(18,2) NOT NULL DEFAULT 0,
    GrossSalary     DECIMAL(18,2) NOT NULL,
    InsuranceDeduct DECIMAL(18,2) NOT NULL DEFAULT 0,
    TaxDeduct       DECIMAL(18,2) NOT NULL DEFAULT 0,
    OtherDeductions DECIMAL(18,2) NOT NULL DEFAULT 0,
    NetSalary       DECIMAL(18,2) NOT NULL,
    IsPaid          BIT NOT NULL DEFAULT 0,
    PaidDate        NVARCHAR(10),
    VoucherId       INT REFERENCES Acc.Vouchers(Id),
    Notes           NVARCHAR(500),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    UNIQUE(EmployeeId, PeriodYear, PeriodMonth)
);

CREATE TABLE Hrm.Commissions (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId  INT NOT NULL REFERENCES Hrm.Employees(Id),
    InvoiceId   INT REFERENCES Sal.SalesInvoices(Id),
    Amount      DECIMAL(18,2) NOT NULL,
    Rate        DECIMAL(5,2),
    PeriodYear  NVARCHAR(4),
    PeriodMonth TINYINT,
    IsIncluded  BIT NOT NULL DEFAULT 0,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- SMS Notifications
CREATE TABLE Cfg.SmsProviders (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(50) NOT NULL,
    ProviderCode NVARCHAR(20) NOT NULL, -- kavenegar/farazsms/melipayamak
    ApiKey      NVARCHAR(200),
    ApiSecret   NVARCHAR(200),
    SenderNumber NVARCHAR(20),
    IsActive    BIT NOT NULL DEFAULT 0,
    IsDefault   BIT NOT NULL DEFAULT 0
);

CREATE TABLE Cfg.SmsTemplates (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Code        NVARCHAR(50) NOT NULL UNIQUE,
    Name        NVARCHAR(100) NOT NULL,
    Template    NVARCHAR(500) NOT NULL,
    IsActive    BIT NOT NULL DEFAULT 1
);

CREATE TABLE Cfg.SmsLogs (
    Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
    ProviderId  INT REFERENCES Cfg.SmsProviders(Id),
    Mobile      NVARCHAR(15) NOT NULL,
    Message     NVARCHAR(500) NOT NULL,
    Status      NVARCHAR(20) NOT NULL DEFAULT 'ارسال شده',
    Response    NVARCHAR(500),
    SentAt      DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- Settings
CREATE TABLE Cfg.AppSettings (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT REFERENCES Cfg.Companies(Id),
    [Key]       NVARCHAR(100) NOT NULL,
    [Value]     NVARCHAR(MAX),
    [Group]     NVARCHAR(50),
    Description NVARCHAR(200),
    UNIQUE(CompanyId, [Key])
);

CREATE TABLE Cfg.BackupHistory (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    BackupType      NVARCHAR(20) NOT NULL, -- خودکار/دستی
    FilePath        NVARCHAR(500) NOT NULL,
    FileSize        BIGINT,
    Status          NVARCHAR(20) NOT NULL,
    ErrorMessage    NVARCHAR(500),
    CreatedByUserId INT REFERENCES Sec.Users(Id),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE()
);

GO
