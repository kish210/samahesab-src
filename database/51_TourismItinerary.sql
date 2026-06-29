-- =============================================================================
-- برنامه‌ریزیِ اقامتی (زیرمجموعهٔ ماژولِ گردشگری). schema: Tur (همان گردشگری) با نام‌جدول‌های Itinerary*.
-- جداولِ AuditableEntity (ItineraryProducts/ItineraryProductSessions/GuestItineraries) ستونِ
-- CompanyId/CreatedAt/UpdatedAt دارند؛ ItineraryStops فرزند (BaseEntity) و بدونِ این ستون‌ها.
-- ستون‌های CreatedByUserId/UpdatedByUserId توسطِ قراردادِ سراسریِ ApplicationDbContext Ignore می‌شوند.
-- idempotent (IF OBJECT_ID/sys.indexes IS NULL)؛ GO-split؛ بدونِ USE. (schema Tur در 42_Tourism ساخته شده.)
-- =============================================================================

IF SCHEMA_ID('Tur') IS NULL EXEC('CREATE SCHEMA Tur');
GO

-- ── محصول/خدمتِ اقامتی ──
IF OBJECT_ID('Tur.ItineraryProducts', 'U') IS NULL
CREATE TABLE Tur.ItineraryProducts (
    Id              int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId       int            NOT NULL,
    Name            nvarchar(200)  NOT NULL,
    SupplierPartyId int            NULL,
    SalePrice       decimal(18,2)  NOT NULL CONSTRAINT DF_TurItPrd_Sale DEFAULT 0,
    Cost            decimal(18,2)  NOT NULL CONSTRAINT DF_TurItPrd_Cost DEFAULT 0,
    Capacity        int            NOT NULL CONSTRAINT DF_TurItPrd_Cap  DEFAULT 0,
    Active          bit            NOT NULL CONSTRAINT DF_TurItPrd_Active  DEFAULT 1,
    MarketerCommissionBasis int    NOT NULL CONSTRAINT DF_TurItPrd_ComBasis DEFAULT 2,   -- 0=مبلغ 1=٪فروش 2=٪سود
    MarketerCommissionValue decimal(18,2) NOT NULL CONSTRAINT DF_TurItPrd_ComVal DEFAULT 0,
    CreatedAt       datetime       NOT NULL CONSTRAINT DF_TurItPrd_Created DEFAULT GETDATE(),
    UpdatedAt       datetime       NULL
);
GO

-- افزودنِ ستون‌های پورسانتِ بازاریاب اگر جدول از قبل بدونِ آن‌ها ساخته شده (ارتقای idempotent).
IF COL_LENGTH('Tur.ItineraryProducts', 'MarketerCommissionBasis') IS NULL
    ALTER TABLE Tur.ItineraryProducts ADD MarketerCommissionBasis int NOT NULL CONSTRAINT DF_TurItPrd_ComBasis DEFAULT 2;
GO
IF COL_LENGTH('Tur.ItineraryProducts', 'MarketerCommissionValue') IS NULL
    ALTER TABLE Tur.ItineraryProducts ADD MarketerCommissionValue decimal(18,2) NOT NULL CONSTRAINT DF_TurItPrd_ComVal DEFAULT 0;
GO

-- ── سانسِ زمانیِ محصول ──
IF OBJECT_ID('Tur.ItineraryProductSessions', 'U') IS NULL
CREATE TABLE Tur.ItineraryProductSessions (
    Id          int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId   int           NOT NULL,
    ProductId   int           NOT NULL,
    Label       nvarchar(100) NOT NULL,
    StartMinute int           NOT NULL,
    EndMinute   int           NOT NULL,
    Capacity    int           NOT NULL CONSTRAINT DF_TurItSes_Cap    DEFAULT 0,
    Active      bit           NOT NULL CONSTRAINT DF_TurItSes_Active  DEFAULT 1,
    CreatedAt   datetime      NOT NULL CONSTRAINT DF_TurItSes_Created DEFAULT GETDATE(),
    UpdatedAt   datetime      NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TurItSes_Product' AND object_id = OBJECT_ID('Tur.ItineraryProductSessions'))
CREATE INDEX IX_TurItSes_Product ON Tur.ItineraryProductSessions(ProductId);
GO

-- ── برنامهٔ اقامتیِ مهمان (سرسند) ──
IF OBJECT_ID('Tur.GuestItineraries', 'U') IS NULL
CREATE TABLE Tur.GuestItineraries (
    Id           int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId    int            NOT NULL,
    GuestName    nvarchar(200)  NOT NULL,
    GuestPartyId int            NULL,
    Token        nvarchar(64)   NOT NULL,
    Days         int            NOT NULL,
    CreatedDate  nvarchar(10)   NOT NULL,
    Status       int            NOT NULL CONSTRAINT DF_TurGItin_Status  DEFAULT 0,
    Notes        nvarchar(1000) NULL,
    CreatedAt    datetime       NOT NULL CONSTRAINT DF_TurGItin_Created DEFAULT GETDATE(),
    UpdatedAt    datetime       NULL
);
GO

-- توکنِ یکتا = کلیدِ لینکِ پنلِ مهمان (ضدِ تداخل/حدس).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TurGItin_Token' AND object_id = OBJECT_ID('Tur.GuestItineraries'))
CREATE UNIQUE INDEX UX_TurGItin_Token ON Tur.GuestItineraries(Token);
GO

-- ── قلمِ برنامه (فرزند) ──
IF OBJECT_ID('Tur.ItineraryStops', 'U') IS NULL
CREATE TABLE Tur.ItineraryStops (
    Id          int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ItineraryId int           NOT NULL,
    ProductId   int           NOT NULL,
    SessionId   int           NOT NULL,
    DayNumber   int           NOT NULL,
    SortOrder   int           NOT NULL CONSTRAINT DF_TurItStop_Sort DEFAULT 0,
    StartMinute int           NOT NULL,
    EndMinute   int           NOT NULL,
    SalePrice   decimal(18,2) NOT NULL CONSTRAINT DF_TurItStop_Sale DEFAULT 0,
    Cost        decimal(18,2) NOT NULL CONSTRAINT DF_TurItStop_Cost DEFAULT 0,
    CONSTRAINT FK_TurItStop_Itinerary FOREIGN KEY (ItineraryId) REFERENCES Tur.GuestItineraries(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TurItStop_Itinerary' AND object_id = OBJECT_ID('Tur.ItineraryStops'))
CREATE INDEX IX_TurItStop_Itinerary ON Tur.ItineraryStops(ItineraryId);
GO
