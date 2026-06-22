-- =============================================================================
-- TUR-C1-1 — اسکیمـای ماژولِ گردشگری (مدلِ ودیعهٔ تأمین‌کننده)، schema Tur.
-- جداولِ AuditableEntity ستونِ CreatedAt/UpdatedAt دارند (EF لازم دارد)؛ SaleLines/SalePassengers
-- (BaseEntity) ندارند. idempotent؛ GO-split؛ بدونِ USE (اتصال خودش DBِ درست را هدف گرفته).
-- =============================================================================

IF SCHEMA_ID('Tur') IS NULL EXEC('CREATE SCHEMA Tur');
GO

-- ── گروهِ محصول ──
IF OBJECT_ID('Tur.ProductGroups', 'U') IS NULL
CREATE TABLE Tur.ProductGroups (
    Id        int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId int           NOT NULL,
    Name      nvarchar(150) NOT NULL,
    Active    bit           NOT NULL CONSTRAINT DF_TurGrp_Active DEFAULT 1,
    CreatedAt datetime      NOT NULL CONSTRAINT DF_TurGrp_Created DEFAULT GETDATE(),
    UpdatedAt datetime      NULL
);
GO

-- ── محصول/خدمت ──
IF OBJECT_ID('Tur.Products', 'U') IS NULL
CREATE TABLE Tur.Products (
    Id                    int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId             int           NOT NULL,
    Name                  nvarchar(200) NOT NULL,
    SupplierPartyId       int           NOT NULL,
    PurchasePrice         decimal(18,2) NOT NULL CONSTRAINT DF_TurPrd_Purchase DEFAULT 0,
    DefaultSalePrice      decimal(18,2) NOT NULL CONSTRAINT DF_TurPrd_Sale     DEFAULT 0,
    ProductGroupId        int           NULL,
    RequiresPassengerList bit           NOT NULL CONSTRAINT DF_TurPrd_Pax       DEFAULT 0,
    Active                bit           NOT NULL CONSTRAINT DF_TurPrd_Active     DEFAULT 1,
    CreatedAt             datetime      NOT NULL CONSTRAINT DF_TurPrd_Created    DEFAULT GETDATE(),
    UpdatedAt             datetime      NULL
);
GO

-- ── شارژِ ودیعهٔ تأمین‌کننده ──
IF OBJECT_ID('Tur.SupplierDeposits', 'U') IS NULL
CREATE TABLE Tur.SupplierDeposits (
    Id              int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId       int           NOT NULL,
    SupplierPartyId int           NOT NULL,
    Amount          decimal(18,2) NOT NULL,
    Date            nvarchar(10)  NOT NULL,
    PaymentMethod   nvarchar(20)  NOT NULL CONSTRAINT DF_TurDep_Method DEFAULT N'بانک',
    VoucherId       int           NULL,
    Note            nvarchar(500) NULL,
    CreatedAt       datetime      NOT NULL CONSTRAINT DF_TurDep_Created DEFAULT GETDATE(),
    UpdatedAt       datetime      NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TurDeposits_Company_Supplier' AND object_id=OBJECT_ID('Tur.SupplierDeposits'))
    CREATE INDEX IX_TurDeposits_Company_Supplier ON Tur.SupplierDeposits (CompanyId, SupplierPartyId);
GO

-- ── تنظیمات (نگاشتِ حساب‌ها + پرچم‌ها) ──
IF OBJECT_ID('Tur.Settings', 'U') IS NULL
CREATE TABLE Tur.Settings (
    Id                           int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId                    int          NOT NULL,
    CashAccountId                int          NULL,
    ReceivableAccountId          int          NULL,
    RevenueAccountId             int          NULL,
    CogsAccountId                int          NULL,
    SupplierDepositAccountId     int          NULL,
    SalesDiscountAccountId       int          NULL,
    DepositDifferenceAccountId   int          NULL,
    CommissionExpenseAccountId   int          NULL,
    SalespersonPayableAccountId  int          NULL,
    BankAccountId                int          NULL,
    SaleBaseAfterDiscountDefault bit          NOT NULL CONSTRAINT DF_TurSet_AfterDisc DEFAULT 1,
    LowDepositThreshold          decimal(18,2) NOT NULL CONSTRAINT DF_TurSet_LowDep  DEFAULT 0,
    PostPerSale                  bit          NOT NULL CONSTRAINT DF_TurSet_PerSale   DEFAULT 1,
    CommissionThroughPayroll     bit          NOT NULL CONSTRAINT DF_TurSet_Payroll   DEFAULT 1,
    CreatedAt                    datetime     NOT NULL CONSTRAINT DF_TurSet_Created    DEFAULT GETDATE(),
    UpdatedAt                    datetime     NULL,
    CONSTRAINT UQ_TurSettings_Company UNIQUE (CompanyId)
);
GO

-- ── قواعدِ پورسانت ──
IF OBJECT_ID('Tur.CommissionRules', 'U') IS NULL
CREATE TABLE Tur.CommissionRules (
    Id                    int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId             int           NOT NULL,
    SalespersonPartyId    int           NOT NULL,
    ProductId             int           NULL,
    ProductGroupId        int           NULL,
    Basis                 int           NOT NULL CONSTRAINT DF_TurCmR_Basis DEFAULT 0,
    Rate                  decimal(18,4) NOT NULL CONSTRAINT DF_TurCmR_Rate  DEFAULT 0,
    SaleBaseAfterDiscount bit           NOT NULL CONSTRAINT DF_TurCmR_After DEFAULT 1,
    EffectiveFrom         nvarchar(10)  NOT NULL,
    EffectiveTo           nvarchar(10)  NULL,
    Active                bit           NOT NULL CONSTRAINT DF_TurCmR_Active DEFAULT 1,
    CreatedAt             datetime      NOT NULL CONSTRAINT DF_TurCmR_Created DEFAULT GETDATE(),
    UpdatedAt             datetime      NULL
);
GO

-- ── رکوردهای پورسانت ──
IF OBJECT_ID('Tur.CommissionEntries', 'U') IS NULL
CREATE TABLE Tur.CommissionEntries (
    Id                 int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId          int           NOT NULL,
    SaleLineId         int           NOT NULL,
    SalespersonPartyId int           NOT NULL,
    RuleId             int           NULL,
    Basis              int           NOT NULL CONSTRAINT DF_TurCmE_Basis DEFAULT 0,
    BaseAmount         decimal(18,2) NOT NULL CONSTRAINT DF_TurCmE_Base  DEFAULT 0,
    Rate               decimal(18,4) NOT NULL CONSTRAINT DF_TurCmE_Rate  DEFAULT 0,
    CommissionAmount   decimal(18,2) NOT NULL CONSTRAINT DF_TurCmE_Amt   DEFAULT 0,
    PersianYearMonth   nvarchar(6)   NOT NULL,
    CreatedAt          datetime      NOT NULL CONSTRAINT DF_TurCmE_Created DEFAULT GETDATE(),
    UpdatedAt          datetime      NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TurCommEntries_Sp_Month' AND object_id=OBJECT_ID('Tur.CommissionEntries'))
    CREATE INDEX IX_TurCommEntries_Sp_Month ON Tur.CommissionEntries (CompanyId, SalespersonPartyId, PersianYearMonth);
GO

-- ── گزارشِ روزانهٔ تأمین‌کننده ──
IF OBJECT_ID('Tur.SupplierDailyReports', 'U') IS NULL
CREATE TABLE Tur.SupplierDailyReports (
    Id                    int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId             int           NOT NULL,
    SupplierPartyId       int           NOT NULL,
    Date                  nvarchar(10)  NOT NULL,
    TotalCost             decimal(18,2) NOT NULL CONSTRAINT DF_TurDR_Cost DEFAULT 0,
    LineCount             int           NOT NULL CONSTRAINT DF_TurDR_Lines DEFAULT 0,
    PassengerCount        int           NOT NULL CONSTRAINT DF_TurDR_Pax   DEFAULT 0,
    Status                int           NOT NULL CONSTRAINT DF_TurDR_Status DEFAULT 0,
    SupplierDeductedAmount decimal(18,2) NULL,
    AdjustmentVoucherId   int           NULL,
    Note                  nvarchar(500) NULL,
    CreatedAt             datetime      NOT NULL CONSTRAINT DF_TurDR_Created DEFAULT GETDATE(),
    UpdatedAt             datetime      NULL,
    CONSTRAINT UQ_TurDailyReport UNIQUE (CompanyId, SupplierPartyId, Date)
);
GO

-- ── سربرگِ فروش ──
IF OBJECT_ID('Tur.Sales', 'U') IS NULL
CREATE TABLE Tur.Sales (
    Id                 int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId          int           NOT NULL,
    BranchId           int           NOT NULL,
    Date               nvarchar(10)  NOT NULL,
    CustomerPartyId    int           NULL,
    SalespersonPartyId int           NOT NULL,
    PaymentMethod      nvarchar(20)  NOT NULL CONSTRAINT DF_TurSal_Method DEFAULT N'نقدی',
    TotalSale          decimal(18,2) NOT NULL CONSTRAINT DF_TurSal_Sale  DEFAULT 0,
    TotalDiscount      decimal(18,2) NOT NULL CONSTRAINT DF_TurSal_Disc  DEFAULT 0,
    TotalCost          decimal(18,2) NOT NULL CONSTRAINT DF_TurSal_Cost  DEFAULT 0,
    TotalProfit        decimal(18,2) NOT NULL CONSTRAINT DF_TurSal_Profit DEFAULT 0,
    VoucherId          int           NULL,
    Note               nvarchar(500) NULL,
    CreatedAt          datetime      NOT NULL CONSTRAINT DF_TurSal_Created DEFAULT GETDATE(),
    UpdatedAt          datetime      NULL
);
GO

-- ── خطوطِ فروش (BaseEntity — بدونِ ستونِ ممیزی) ──
IF OBJECT_ID('Tur.SaleLines', 'U') IS NULL
CREATE TABLE Tur.SaleLines (
    Id              int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    SaleId          int           NOT NULL,
    ProductId       int           NOT NULL,
    SupplierPartyId int           NOT NULL,
    Quantity        decimal(18,2) NOT NULL,
    UnitSalePrice   decimal(18,2) NOT NULL,
    DiscountAmount  decimal(18,2) NOT NULL CONSTRAINT DF_TurLn_Disc DEFAULT 0,
    UnitCost        decimal(18,2) NOT NULL,
    LineProfit      decimal(18,2) NOT NULL CONSTRAINT DF_TurLn_Profit DEFAULT 0,
    CONSTRAINT FK_TurSaleLines_Sale FOREIGN KEY (SaleId) REFERENCES Tur.Sales(Id)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TurSaleLines_Sale' AND object_id=OBJECT_ID('Tur.SaleLines'))
    CREATE INDEX IX_TurSaleLines_Sale ON Tur.SaleLines (SaleId);
GO

-- ── مسافران (BaseEntity) ──
IF OBJECT_ID('Tur.SalePassengers', 'U') IS NULL
CREATE TABLE Tur.SalePassengers (
    Id                   int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    SaleLineId           int           NOT NULL,
    FullName             nvarchar(200) NOT NULL,
    NationalIdOrPassport nvarchar(30)  NULL,
    Phone                nvarchar(30)  NULL,
    CONSTRAINT FK_TurPassengers_Line FOREIGN KEY (SaleLineId) REFERENCES Tur.SaleLines(Id)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TurPassengers_Line' AND object_id=OBJECT_ID('Tur.SalePassengers'))
    CREATE INDEX IX_TurPassengers_Line ON Tur.SalePassengers (SaleLineId);
GO
