-- =============================================================================
-- ایستگاه‌های چاپ/فیش‌پرینترِ رستوران + نگاشتِ کالا→ایستگاه. schema: Rst.
-- هر دو AuditableEntity (CompanyId/CreatedAt/UpdatedAt). idempotent؛ GO-split؛ بدونِ USE.
-- =============================================================================

IF SCHEMA_ID('Rst') IS NULL EXEC('CREATE SCHEMA Rst');
GO

-- ── ایستگاهِ چاپ (یک فیش‌پرینتر برای یک بخش) ──
IF OBJECT_ID('Rst.PrintStations', 'U') IS NULL
CREATE TABLE Rst.PrintStations (
    Id          int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId   int           NOT NULL,
    Name        nvarchar(100) NOT NULL,
    PrinterName nvarchar(200) NOT NULL CONSTRAINT DF_RstPS_Printer DEFAULT '',
    IsDefault   bit           NOT NULL CONSTRAINT DF_RstPS_Default  DEFAULT 0,
    Active      bit           NOT NULL CONSTRAINT DF_RstPS_Active   DEFAULT 1,
    CreatedAt   datetime      NOT NULL CONSTRAINT DF_RstPS_Created  DEFAULT GETDATE(),
    UpdatedAt   datetime      NULL
);
GO

-- ── نگاشتِ کالا → ایستگاه (هر کالا حداکثر یک ایستگاه) ──
IF OBJECT_ID('Rst.ProductStationMaps', 'U') IS NULL
CREATE TABLE Rst.ProductStationMaps (
    Id        int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId int      NOT NULL,
    ProductId int      NOT NULL,
    StationId int      NOT NULL,
    CreatedAt datetime NOT NULL CONSTRAINT DF_RstPSM_Created DEFAULT GETDATE(),
    UpdatedAt datetime NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_RstPSM_Company_Product' AND object_id = OBJECT_ID('Rst.ProductStationMaps'))
CREATE UNIQUE INDEX UX_RstPSM_Company_Product ON Rst.ProductStationMaps(CompanyId, ProductId);
GO
