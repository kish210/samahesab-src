-- =============================================================================
-- قراردادهایِ خدماتیِ تکرارشونده (ماژولِ پیمانکاری، schema Con) — جایگزینِ فیچرِ
-- حذف‌شدهٔ «فاکتورِ تکرارشونده». idempotent؛ GO-split؛ بدونِ USE.
-- =============================================================================

IF SCHEMA_ID('Con') IS NULL EXEC('CREATE SCHEMA Con');
GO

IF OBJECT_ID('Con.ServiceContracts', 'U') IS NULL
CREATE TABLE Con.ServiceContracts (
    Id               int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId        int           NOT NULL,
    BranchId         int           NOT NULL,
    Title            nvarchar(250) NOT NULL,
    CustomerId       int           NOT NULL,
    WarehouseId      int           NOT NULL,
    PriceLevel       nvarchar(20)  NOT NULL CONSTRAINT DF_SvcCon_Price   DEFAULT N'خرده',
    ServiceProductId int           NOT NULL,
    MonthlyAmount    decimal(18,2) NOT NULL CONSTRAINT DF_SvcCon_Amount DEFAULT 0,
    TaxPct           decimal(18,2) NOT NULL CONSTRAINT DF_SvcCon_Tax     DEFAULT 0,
    Frequency        int           NOT NULL CONSTRAINT DF_SvcCon_Freq    DEFAULT 0,
    NextInvoiceDate  nvarchar(10)  NOT NULL,
    LastGeneratedDate nvarchar(10) NULL,
    IsActive         bit           NOT NULL CONSTRAINT DF_SvcCon_Active  DEFAULT 1,
    Description      nvarchar(500) NULL,
    CreatedAt        datetime      NOT NULL CONSTRAINT DF_SvcCon_Created DEFAULT GETDATE(),
    UpdatedAt        datetime      NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SvcContracts_Company' AND object_id=OBJECT_ID('Con.ServiceContracts'))
    CREATE INDEX IX_SvcContracts_Company ON Con.ServiceContracts (CompanyId, IsActive);
GO

IF OBJECT_ID('Con.ServiceContractExtraItems', 'U') IS NULL
CREATE TABLE Con.ServiceContractExtraItems (
    Id                int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId         int           NOT NULL,
    ServiceContractId int           NOT NULL,
    ProductId         int           NOT NULL,
    Quantity          decimal(18,3) NOT NULL CONSTRAINT DF_SvcExtra_Qty  DEFAULT 0,
    UnitPrice         decimal(18,2) NOT NULL CONSTRAINT DF_SvcExtra_Price DEFAULT 0,
    TaxPct            decimal(18,2) NOT NULL CONSTRAINT DF_SvcExtra_Tax   DEFAULT 0,
    IsConsumed        bit           NOT NULL CONSTRAINT DF_SvcExtra_Cons  DEFAULT 0,
    CreatedAt         datetime      NOT NULL CONSTRAINT DF_SvcExtra_Created DEFAULT GETDATE(),
    UpdatedAt         datetime      NULL,
    CONSTRAINT FK_SvcExtraItems_Contract FOREIGN KEY (ServiceContractId) REFERENCES Con.ServiceContracts(Id)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SvcExtraItems_Contract' AND object_id=OBJECT_ID('Con.ServiceContractExtraItems'))
    CREATE INDEX IX_SvcExtraItems_Contract ON Con.ServiceContractExtraItems (ServiceContractId, IsConsumed);
GO

PRINT N'جدول‌هایِ قراردادهایِ خدماتی ایجاد شدند.';
GO
