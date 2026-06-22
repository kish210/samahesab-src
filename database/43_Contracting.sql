-- =============================================================================
-- CON-C1-1 — اسکیمـای ماژولِ پیمانکاری (صورت‌وضعیت/کسورات/پیش‌پرداخت/ضمانت‌نامه)، schema Con.
-- جداولِ AuditableEntity ستونِ CreatedAt/UpdatedAt دارند؛ StatementDeductions (BaseEntity) ندارد.
-- idempotent؛ GO-split؛ بدونِ USE.
-- =============================================================================

IF SCHEMA_ID('Con') IS NULL EXEC('CREATE SCHEMA Con');
GO

-- ── پیمان ──
IF OBJECT_ID('Con.Projects', 'U') IS NULL
CREATE TABLE Con.Projects (
    Id                       int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId                int           NOT NULL,
    Code                     nvarchar(50)  NOT NULL,
    Title                    nvarchar(250) NOT NULL,
    EmployerPartyId          int           NOT NULL,
    ContractType             int           NOT NULL CONSTRAINT DF_ConPrj_Type    DEFAULT 0,
    ContractAmount           decimal(18,2) NOT NULL CONSTRAINT DF_ConPrj_Amount  DEFAULT 0,
    StartDate                nvarchar(10)  NOT NULL,
    DurationDays             int           NOT NULL CONSTRAINT DF_ConPrj_Days     DEFAULT 0,
    AdvancePercent           decimal(9,4)  NOT NULL CONSTRAINT DF_ConPrj_Adv      DEFAULT 0,
    RetentionPercent         decimal(9,4)  NOT NULL CONSTRAINT DF_ConPrj_Ret      DEFAULT 0,
    InsuranceWithholdPercent decimal(9,4)  NOT NULL CONSTRAINT DF_ConPrj_Ins      DEFAULT 0,
    TaxWithholdPercent       decimal(9,4)  NOT NULL CONSTRAINT DF_ConPrj_Tax      DEFAULT 0,
    AdjustmentEnabled        bit           NOT NULL CONSTRAINT DF_ConPrj_Adj      DEFAULT 0,
    ProjectDimensionId       int           NULL,
    Status                   int           NOT NULL CONSTRAINT DF_ConPrj_Status   DEFAULT 0,
    CreatedAt                datetime      NOT NULL CONSTRAINT DF_ConPrj_Created  DEFAULT GETDATE(),
    UpdatedAt                datetime      NULL,
    CONSTRAINT UQ_ConProjects_Company_Code UNIQUE (CompanyId, Code)
);
GO

-- ── صورت‌وضعیت ──
IF OBJECT_ID('Con.Statements', 'U') IS NULL
CREATE TABLE Con.Statements (
    Id                  int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId           int           NOT NULL,
    ContractProjectId   int           NOT NULL,
    Number              int           NOT NULL CONSTRAINT DF_ConSt_Num   DEFAULT 0,
    Type                int           NOT NULL CONSTRAINT DF_ConSt_Type  DEFAULT 0,
    Date                nvarchar(10)  NOT NULL,
    CumulativeGrossWork decimal(18,2) NOT NULL CONSTRAINT DF_ConSt_Cum   DEFAULT 0,
    PreviousCumulative  decimal(18,2) NOT NULL CONSTRAINT DF_ConSt_Prev  DEFAULT 0,
    PeriodWork          decimal(18,2) NOT NULL CONSTRAINT DF_ConSt_Per   DEFAULT 0,
    AdjustmentAmount    decimal(18,2) NOT NULL CONSTRAINT DF_ConSt_Adj   DEFAULT 0,
    MaterialDiffAmount  decimal(18,2) NOT NULL CONSTRAINT DF_ConSt_Mat   DEFAULT 0,
    GrossThisPeriod     decimal(18,2) NOT NULL CONSTRAINT DF_ConSt_Gross DEFAULT 0,
    AdvanceRecovery     decimal(18,2) NOT NULL CONSTRAINT DF_ConSt_AdvR  DEFAULT 0,
    Retention           decimal(18,2) NOT NULL CONSTRAINT DF_ConSt_Ret   DEFAULT 0,
    Insurance           decimal(18,2) NOT NULL CONSTRAINT DF_ConSt_Ins   DEFAULT 0,
    Tax                 decimal(18,2) NOT NULL CONSTRAINT DF_ConSt_Tax   DEFAULT 0,
    Penalty             decimal(18,2) NOT NULL CONSTRAINT DF_ConSt_Pen   DEFAULT 0,
    Other               decimal(18,2) NOT NULL CONSTRAINT DF_ConSt_Oth   DEFAULT 0,
    NetPayable          decimal(18,2) NOT NULL CONSTRAINT DF_ConSt_Net   DEFAULT 0,
    Status              int           NOT NULL CONSTRAINT DF_ConSt_Status DEFAULT 0,
    VoucherId           int           NULL,
    CreatedAt           datetime      NOT NULL CONSTRAINT DF_ConSt_Created DEFAULT GETDATE(),
    UpdatedAt           datetime      NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ConStatements_Project' AND object_id=OBJECT_ID('Con.Statements'))
    CREATE INDEX IX_ConStatements_Project ON Con.Statements (CompanyId, ContractProjectId);
GO

-- ── کسوراتِ صورت‌وضعیت (BaseEntity — بدونِ ستونِ ممیزی) ──
IF OBJECT_ID('Con.StatementDeductions', 'U') IS NULL
CREATE TABLE Con.StatementDeductions (
    Id          int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    StatementId int           NOT NULL,
    Type        int           NOT NULL CONSTRAINT DF_ConDed_Type DEFAULT 0,
    Base        decimal(18,2) NOT NULL CONSTRAINT DF_ConDed_Base DEFAULT 0,
    Rate        decimal(9,4)  NOT NULL CONSTRAINT DF_ConDed_Rate DEFAULT 0,
    Amount      decimal(18,2) NOT NULL CONSTRAINT DF_ConDed_Amt  DEFAULT 0,
    AccountId   int           NOT NULL CONSTRAINT DF_ConDed_Acc  DEFAULT 0,
    CONSTRAINT FK_ConDeductions_Statement FOREIGN KEY (StatementId) REFERENCES Con.Statements(Id)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ConDeductions_Statement' AND object_id=OBJECT_ID('Con.StatementDeductions'))
    CREATE INDEX IX_ConDeductions_Statement ON Con.StatementDeductions (StatementId);
GO

-- ── پیش‌پرداخت ──
IF OBJECT_ID('Con.AdvancePayments', 'U') IS NULL
CREATE TABLE Con.AdvancePayments (
    Id                int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId         int           NOT NULL,
    ContractProjectId int           NOT NULL,
    Amount            decimal(18,2) NOT NULL,
    Date              nvarchar(10)  NOT NULL,
    RecoveredToDate   decimal(18,2) NOT NULL CONSTRAINT DF_ConAdv_Rec DEFAULT 0,
    PaymentMethod     nvarchar(20)  NOT NULL CONSTRAINT DF_ConAdv_Method DEFAULT N'بانک',
    VoucherId         int           NULL,
    Note              nvarchar(500) NULL,
    CreatedAt         datetime      NOT NULL CONSTRAINT DF_ConAdv_Created DEFAULT GETDATE(),
    UpdatedAt         datetime      NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ConAdvances_Project' AND object_id=OBJECT_ID('Con.AdvancePayments'))
    CREATE INDEX IX_ConAdvances_Project ON Con.AdvancePayments (CompanyId, ContractProjectId);
GO

-- ── ضمانت‌نامه ──
IF OBJECT_ID('Con.Guarantees', 'U') IS NULL
CREATE TABLE Con.Guarantees (
    Id                int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId         int           NOT NULL,
    ContractProjectId int           NOT NULL,
    Type              int           NOT NULL CONSTRAINT DF_ConGu_Type   DEFAULT 0,
    Bank              nvarchar(150) NOT NULL CONSTRAINT DF_ConGu_Bank   DEFAULT N'—',
    Amount            decimal(18,2) NOT NULL,
    IssueDate         nvarchar(10)  NULL,
    ExpiryDate        nvarchar(10)  NOT NULL,
    Status            int           NOT NULL CONSTRAINT DF_ConGu_Status DEFAULT 0,
    Note              nvarchar(500) NULL,
    CreatedAt         datetime      NOT NULL CONSTRAINT DF_ConGu_Created DEFAULT GETDATE(),
    UpdatedAt         datetime      NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ConGuarantees_Project' AND object_id=OBJECT_ID('Con.Guarantees'))
    CREATE INDEX IX_ConGuarantees_Project ON Con.Guarantees (CompanyId, ContractProjectId);
GO

-- ── تنظیمات ──
IF OBJECT_ID('Con.Settings', 'U') IS NULL
CREATE TABLE Con.Settings (
    Id                              int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId                       int          NOT NULL,
    ReceivableAccountId             int          NULL,
    RetentionDepositAccountId       int          NULL,
    InsuranceDepositAccountId       int          NULL,
    PrepaidTaxAccountId             int          NULL,
    AdvanceLiabilityAccountId       int          NULL,
    PenaltyExpenseAccountId         int          NULL,
    RevenueAccountId                int          NULL,
    BankAccountId                   int          NULL,
    DefaultAdvancePercent           decimal(9,4) NOT NULL CONSTRAINT DF_ConSet_Adv DEFAULT 0,
    DefaultRetentionPercent         decimal(9,4) NOT NULL CONSTRAINT DF_ConSet_Ret DEFAULT 0,
    DefaultInsuranceWithholdPercent decimal(9,4) NOT NULL CONSTRAINT DF_ConSet_Ins DEFAULT 0,
    DefaultTaxWithholdPercent       decimal(9,4) NOT NULL CONSTRAINT DF_ConSet_Tax DEFAULT 0,
    UseCostCenterAsDimension        bit          NOT NULL CONSTRAINT DF_ConSet_Dim DEFAULT 0,
    CreatedAt                       datetime     NOT NULL CONSTRAINT DF_ConSet_Created DEFAULT GETDATE(),
    UpdatedAt                       datetime     NULL,
    CONSTRAINT UQ_ConSettings_Company UNIQUE (CompanyId)
);
GO
