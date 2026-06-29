-- =============================================================================
-- ماژولِ TourismItinerary (برنامه‌ریزی اقامتی گردشگری). schema: Tit.
-- جداولِ AuditableEntity (Products/ProductSessions/GuestItineraries) ستونِ CompanyId/CreatedAt/UpdatedAt
-- دارند؛ ItineraryStops فرزند (BaseEntity) و بدونِ این ستون‌ها (فیلترِ شرکت ندارد).
-- ستون‌های CreatedByUserId/UpdatedByUserId توسطِ قراردادِ سراسریِ ApplicationDbContext Ignore می‌شوند.
-- idempotent (IF OBJECT_ID/COL_LENGTH IS NULL)؛ GO-split؛ بدونِ USE.
-- =============================================================================

IF SCHEMA_ID('Tit') IS NULL EXEC('CREATE SCHEMA Tit');
GO

-- ── محصول/خدمتِ اقامتی ──
IF OBJECT_ID('Tit.Products', 'U') IS NULL
CREATE TABLE Tit.Products (
    Id              int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId       int            NOT NULL,
    Name            nvarchar(200)  NOT NULL,
    SupplierPartyId int            NULL,
    SalePrice       decimal(18,2)  NOT NULL CONSTRAINT DF_TitPrd_Sale DEFAULT 0,
    Cost            decimal(18,2)  NOT NULL CONSTRAINT DF_TitPrd_Cost DEFAULT 0,
    Capacity        int            NOT NULL CONSTRAINT DF_TitPrd_Cap  DEFAULT 0,
    Active          bit            NOT NULL CONSTRAINT DF_TitPrd_Active  DEFAULT 1,
    CreatedAt       datetime       NOT NULL CONSTRAINT DF_TitPrd_Created DEFAULT GETDATE(),
    UpdatedAt       datetime       NULL
);
GO

-- ── سانسِ زمانیِ محصول ──
IF OBJECT_ID('Tit.ProductSessions', 'U') IS NULL
CREATE TABLE Tit.ProductSessions (
    Id          int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId   int           NOT NULL,
    ProductId   int           NOT NULL,
    Label       nvarchar(100) NOT NULL,
    StartMinute int           NOT NULL,
    EndMinute   int           NOT NULL,
    Capacity    int           NOT NULL CONSTRAINT DF_TitSes_Cap    DEFAULT 0,
    Active      bit           NOT NULL CONSTRAINT DF_TitSes_Active  DEFAULT 1,
    CreatedAt   datetime      NOT NULL CONSTRAINT DF_TitSes_Created DEFAULT GETDATE(),
    UpdatedAt   datetime      NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TitSes_Product' AND object_id = OBJECT_ID('Tit.ProductSessions'))
CREATE INDEX IX_TitSes_Product ON Tit.ProductSessions(ProductId);
GO

-- ── برنامهٔ اقامتیِ مهمان (سرسند) ──
IF OBJECT_ID('Tit.GuestItineraries', 'U') IS NULL
CREATE TABLE Tit.GuestItineraries (
    Id           int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId    int            NOT NULL,
    GuestName    nvarchar(200)  NOT NULL,
    GuestPartyId int            NULL,
    Token        nvarchar(64)   NOT NULL,
    Days         int            NOT NULL,
    CreatedDate  nvarchar(10)   NOT NULL,
    Status       int            NOT NULL CONSTRAINT DF_TitItin_Status  DEFAULT 0,
    Notes        nvarchar(1000) NULL,
    CreatedAt    datetime       NOT NULL CONSTRAINT DF_TitItin_Created DEFAULT GETDATE(),
    UpdatedAt    datetime       NULL
);
GO

-- توکنِ یکتا = کلیدِ لینکِ پنلِ مهمان (ضدِ تداخل/حدس).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TitItin_Token' AND object_id = OBJECT_ID('Tit.GuestItineraries'))
CREATE UNIQUE INDEX UX_TitItin_Token ON Tit.GuestItineraries(Token);
GO

-- ── قلمِ برنامه (فرزند) ──
IF OBJECT_ID('Tit.ItineraryStops', 'U') IS NULL
CREATE TABLE Tit.ItineraryStops (
    Id          int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ItineraryId int           NOT NULL,
    ProductId   int           NOT NULL,
    SessionId   int           NOT NULL,
    DayNumber   int           NOT NULL,
    SortOrder   int           NOT NULL CONSTRAINT DF_TitStop_Sort DEFAULT 0,
    StartMinute int           NOT NULL,
    EndMinute   int           NOT NULL,
    SalePrice   decimal(18,2) NOT NULL CONSTRAINT DF_TitStop_Sale DEFAULT 0,
    Cost        decimal(18,2) NOT NULL CONSTRAINT DF_TitStop_Cost DEFAULT 0,
    CONSTRAINT FK_TitStop_Itinerary FOREIGN KEY (ItineraryId) REFERENCES Tit.GuestItineraries(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TitStop_Itinerary' AND object_id = OBJECT_ID('Tit.ItineraryStops'))
CREATE INDEX IX_TitStop_Itinerary ON Tit.ItineraryStops(ItineraryId);
GO
