-- =============================================================================
-- PMS-C1-1 — موجودیت‌های هتل / PMS. schema Htl.
-- جداولِ AuditableEntity ستونِ CompanyId/CreatedAt/UpdatedAt دارند (EF لازم دارد)؛
-- ReservationRooms/FolioCharges/FolioPayments فرزند (BaseEntity) و بدونِ این ستون‌ها.
-- idempotent (IF OBJECT_ID IS NULL)؛ GO-split؛ بدونِ USE.
-- نکتهٔ کلیدی: UNIQUE(RoomId, Date) روی RoomNightBlocks → ضدِ رزروِ هم‌زمانِ یک اتاق-شب.
-- =============================================================================

IF SCHEMA_ID('Htl') IS NULL EXEC('CREATE SCHEMA Htl');
GO

-- ── نوعِ اتاق ──
IF OBJECT_ID('Htl.RoomTypes', 'U') IS NULL
CREATE TABLE Htl.RoomTypes (
    Id              int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId       int           NOT NULL,
    Code            nvarchar(50)  NOT NULL,
    Name            nvarchar(150) NOT NULL,
    BaseCapacity    int           NOT NULL CONSTRAINT DF_HtlRT_Cap   DEFAULT 2,
    ExtraBedAllowed bit           NOT NULL CONSTRAINT DF_HtlRT_Xbed  DEFAULT 0,
    Active          bit           NOT NULL CONSTRAINT DF_HtlRT_Active DEFAULT 1,
    CreatedAt       datetime      NOT NULL CONSTRAINT DF_HtlRT_Created DEFAULT GETDATE(),
    UpdatedAt       datetime      NULL
);
GO

-- ── اتاقِ فیزیکی ──
IF OBJECT_ID('Htl.Rooms', 'U') IS NULL
CREATE TABLE Htl.Rooms (
    Id         int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId  int           NOT NULL,
    RoomTypeId int           NOT NULL,
    Number     nvarchar(30)  NOT NULL,
    Floor      nvarchar(30)  NULL,
    Status     int           NOT NULL CONSTRAINT DF_HtlRoom_Status DEFAULT 0,
    Active     bit           NOT NULL CONSTRAINT DF_HtlRoom_Active DEFAULT 1,
    CreatedAt  datetime      NOT NULL CONSTRAINT DF_HtlRoom_Created DEFAULT GETDATE(),
    UpdatedAt  datetime      NULL
);
GO

-- ── پلنِ نرخ ──
IF OBJECT_ID('Htl.RatePlans', 'U') IS NULL
CREATE TABLE Htl.RatePlans (
    Id               int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId        int           NOT NULL,
    RoomTypeId       int           NOT NULL,
    ValidFrom        nvarchar(10)  NOT NULL,
    ValidTo          nvarchar(10)  NOT NULL,
    NightlyRate      decimal(18,2) NOT NULL CONSTRAINT DF_HtlRP_Rate    DEFAULT 0,
    WeekendSurcharge decimal(18,2) NOT NULL CONSTRAINT DF_HtlRP_Weekend DEFAULT 0,
    HolidaySurcharge decimal(18,2) NOT NULL CONSTRAINT DF_HtlRP_Holiday DEFAULT 0,
    IncludesBreakfast bit          NOT NULL CONSTRAINT DF_HtlRP_Bfast   DEFAULT 0,
    Active           bit           NOT NULL CONSTRAINT DF_HtlRP_Active  DEFAULT 1,
    CreatedAt        datetime      NOT NULL CONSTRAINT DF_HtlRP_Created DEFAULT GETDATE(),
    UpdatedAt        datetime      NULL
);
GO

-- ── سربرگِ رزرو (شعبه‌ای) ──
IF OBJECT_ID('Htl.Reservations', 'U') IS NULL
CREATE TABLE Htl.Reservations (
    Id             int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId      int           NOT NULL,
    BranchId       int           NOT NULL,
    GuestPartyId   int           NOT NULL,
    CompanyPartyId int           NULL,
    AgentPartyId   int           NULL,
    Source         int           NOT NULL CONSTRAINT DF_HtlRes_Source  DEFAULT 0,
    CheckInDate    nvarchar(10)  NOT NULL,
    CheckOutDate   nvarchar(10)  NOT NULL,
    Nights         int           NOT NULL CONSTRAINT DF_HtlRes_Nights  DEFAULT 1,
    Adults         int           NOT NULL CONSTRAINT DF_HtlRes_Adults  DEFAULT 1,
    Children       int           NOT NULL CONSTRAINT DF_HtlRes_Kids    DEFAULT 0,
    Status         int           NOT NULL CONSTRAINT DF_HtlRes_Status  DEFAULT 0,
    Notes          nvarchar(max) NULL,
    CreatedAt      datetime      NOT NULL CONSTRAINT DF_HtlRes_Created DEFAULT GETDATE(),
    UpdatedAt      datetime      NULL
);
GO

-- ── خطِ اتاقِ رزرو (فرزند) ──
IF OBJECT_ID('Htl.ReservationRooms', 'U') IS NULL
CREATE TABLE Htl.ReservationRooms (
    Id               int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ReservationId    int           NOT NULL,
    RoomTypeId       int           NOT NULL,
    RoomId           int           NULL,
    RatePlanSnapshot nvarchar(200) NULL,
    RatePerNight     decimal(18,2) NOT NULL CONSTRAINT DF_HtlRR_Rate DEFAULT 0,
    ExtraBeds        int           NOT NULL CONSTRAINT DF_HtlRR_Xbed DEFAULT 0
);
GO

-- ── اتاق-شبِ رزروشده — UNIQUE(RoomId, Date) ضدِ تداخل ──
IF OBJECT_ID('Htl.RoomNightBlocks', 'U') IS NULL
CREATE TABLE Htl.RoomNightBlocks (
    Id                int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId         int          NOT NULL,
    ReservationRoomId int          NOT NULL,
    RoomId            int          NOT NULL,
    [Date]            nvarchar(10) NOT NULL,
    CreatedAt         datetime     NOT NULL CONSTRAINT DF_HtlRNB_Created DEFAULT GETDATE(),
    UpdatedAt         datetime     NULL,
    CONSTRAINT UQ_HtlRNB_Room_Date UNIQUE (RoomId, [Date])
);
GO

-- ── فولیو (صورتحسابِ مهمان) ──
IF OBJECT_ID('Htl.Folios', 'U') IS NULL
CREATE TABLE Htl.Folios (
    Id             int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId      int           NOT NULL,
    ReservationId  int           NOT NULL,
    RoomId         int           NULL,
    OpenDate       nvarchar(10)  NOT NULL,
    CloseDate      nvarchar(10)  NULL,
    Status         int           NOT NULL CONSTRAINT DF_HtlFol_Status   DEFAULT 0,
    TotalCharges   decimal(18,2) NOT NULL CONSTRAINT DF_HtlFol_Charges  DEFAULT 0,
    TotalPayments  decimal(18,2) NOT NULL CONSTRAINT DF_HtlFol_Payments DEFAULT 0,
    AppliedDeposit decimal(18,2) NOT NULL CONSTRAINT DF_HtlFol_Deposit  DEFAULT 0,
    CreatedAt      datetime      NOT NULL CONSTRAINT DF_HtlFol_Created   DEFAULT GETDATE(),
    UpdatedAt      datetime      NULL
);
GO

-- ── ردیفِ شارژِ فولیو (فرزند) ──
IF OBJECT_ID('Htl.FolioCharges', 'U') IS NULL
CREATE TABLE Htl.FolioCharges (
    Id          int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    FolioId     int           NOT NULL,
    Type        int           NOT NULL,
    Amount      decimal(18,2) NOT NULL CONSTRAINT DF_HtlFC_Amount DEFAULT 0,
    Description nvarchar(300) NULL,
    [Date]      nvarchar(10)  NOT NULL
);
GO

-- ── ردیفِ پرداختِ فولیو (فرزند) ──
IF OBJECT_ID('Htl.FolioPayments', 'U') IS NULL
CREATE TABLE Htl.FolioPayments (
    Id          int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    FolioId     int           NOT NULL,
    Method      int           NOT NULL,
    Amount      decimal(18,2) NOT NULL CONSTRAINT DF_HtlFP_Amount DEFAULT 0,
    Description nvarchar(300) NULL,
    [Date]      nvarchar(10)  NOT NULL
);
GO

-- ── ودیعه/پیش‌پرداخت ──
IF OBJECT_ID('Htl.Deposits', 'U') IS NULL
CREATE TABLE Htl.Deposits (
    Id            int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId     int           NOT NULL,
    ReservationId int           NOT NULL,
    Amount        decimal(18,2) NOT NULL CONSTRAINT DF_HtlDep_Amount  DEFAULT 0,
    AppliedAmount decimal(18,2) NOT NULL CONSTRAINT DF_HtlDep_Applied DEFAULT 0,
    [Date]        nvarchar(10)  NOT NULL,
    Status        int           NOT NULL CONSTRAINT DF_HtlDep_Status  DEFAULT 0,
    VoucherId     int           NULL,
    CreatedAt     datetime      NOT NULL CONSTRAINT DF_HtlDep_Created DEFAULT GETDATE(),
    UpdatedAt     datetime      NULL
);
GO

-- ── کارِ هاوس‌کیپینگ ──
IF OBJECT_ID('Htl.HousekeepingTasks', 'U') IS NULL
CREATE TABLE Htl.HousekeepingTasks (
    Id               int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId        int          NOT NULL,
    RoomId           int          NOT NULL,
    Type             int          NOT NULL CONSTRAINT DF_HtlHK_Type   DEFAULT 0,
    [Date]           nvarchar(10) NOT NULL,
    Status           int          NOT NULL CONSTRAINT DF_HtlHK_Status DEFAULT 0,
    AssignedToUserId int          NULL,
    Notes            nvarchar(max) NULL,
    CreatedAt        datetime     NOT NULL CONSTRAINT DF_HtlHK_Created DEFAULT GETDATE(),
    UpdatedAt        datetime     NULL
);
GO

-- ── تیکتِ تعمیرات ──
IF OBJECT_ID('Htl.MaintenanceTickets', 'U') IS NULL
CREATE TABLE Htl.MaintenanceTickets (
    Id          int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId   int           NOT NULL,
    RoomId      int           NOT NULL,
    Title       nvarchar(200) NOT NULL,
    Description nvarchar(max) NULL,
    OpenDate    nvarchar(10)  NOT NULL,
    ResolveDate nvarchar(10)  NULL,
    Status      int           NOT NULL CONSTRAINT DF_HtlMT_Status DEFAULT 0,
    BlocksRoom  bit           NOT NULL CONSTRAINT DF_HtlMT_Blocks DEFAULT 0,
    CreatedAt   datetime      NOT NULL CONSTRAINT DF_HtlMT_Created DEFAULT GETDATE(),
    UpdatedAt   datetime      NULL
);
GO

-- ── اجرای ممیزیِ شبانه — UNIQUE(CompanyId, BusinessDate) برای idempotency ──
IF OBJECT_ID('Htl.NightAuditRuns', 'U') IS NULL
CREATE TABLE Htl.NightAuditRuns (
    Id              int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId       int           NOT NULL,
    BusinessDate    nvarchar(10)  NOT NULL,
    StartedAt       nvarchar(30)  NOT NULL,
    FinishedAt      nvarchar(30)  NULL,
    RunByUserId     int           NOT NULL,
    RoomRevenue     decimal(18,2) NOT NULL CONSTRAINT DF_HtlNA_Room  DEFAULT 0,
    LevyTotal       decimal(18,2) NOT NULL CONSTRAINT DF_HtlNA_Levy  DEFAULT 0,
    FoliosProcessed int           NOT NULL CONSTRAINT DF_HtlNA_Folios DEFAULT 0,
    VoucherId       int           NULL,
    CreatedAt       datetime      NOT NULL CONSTRAINT DF_HtlNA_Created DEFAULT GETDATE(),
    UpdatedAt       datetime      NULL,
    CONSTRAINT UQ_HtlNA_Company_Date UNIQUE (CompanyId, BusinessDate)
);
GO

-- ── تنظیماتِ PMS (نگاشتِ حساب‌ها) — یکتا بر شرکت ──
IF OBJECT_ID('Htl.Settings', 'U') IS NULL
CREATE TABLE Htl.Settings (
    Id                            int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId                     int           NOT NULL,
    RoomRevenueAccountId          int           NOT NULL CONSTRAINT DF_HtlSet_RoomRev   DEFAULT 0,
    LevyPayableAccountId          int           NOT NULL CONSTRAINT DF_HtlSet_Levy       DEFAULT 0,
    FolioReceivableAccountId      int           NOT NULL CONSTRAINT DF_HtlSet_FolioRec   DEFAULT 0,
    DepositLiabilityAccountId     int           NOT NULL CONSTRAINT DF_HtlSet_DepLiab    DEFAULT 0,
    InterDeptFbReceivableAccountId int          NOT NULL CONSTRAINT DF_HtlSet_FbRec      DEFAULT 0,
    CompanyReceivableAccountId    int           NOT NULL CONSTRAINT DF_HtlSet_CoRec      DEFAULT 0,
    BankAccountId                 int           NOT NULL CONSTRAINT DF_HtlSet_Bank       DEFAULT 0,
    FbRevenueCostCenterId         int           NOT NULL CONSTRAINT DF_HtlSet_FbCc       DEFAULT 0,
    RoomRevenueCostCenterId       int           NOT NULL CONSTRAINT DF_HtlSet_RoomCc     DEFAULT 0,
    LevyPercent                   decimal(18,2) NOT NULL CONSTRAINT DF_HtlSet_LevyPct    DEFAULT 0,
    BusinessDayCutoff             nvarchar(10)  NOT NULL CONSTRAINT DF_HtlSet_Cutoff     DEFAULT '06:00',
    NoShowChargeFirstNight        bit           NOT NULL CONSTRAINT DF_HtlSet_NoShow     DEFAULT 1,
    CreatedAt                     datetime      NOT NULL CONSTRAINT DF_HtlSet_Created    DEFAULT GETDATE(),
    UpdatedAt                     datetime      NULL,
    CONSTRAINT UQ_HtlSet_Company UNIQUE (CompanyId)
);
GO
