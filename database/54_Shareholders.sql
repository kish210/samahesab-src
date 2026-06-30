-- =============================================================================
-- سهامداران (دفترِ سهامِ شرکت) — schema Cfg. idempotent؛ بدونِ USE.
-- =============================================================================

IF SCHEMA_ID('Cfg') IS NULL EXEC('CREATE SCHEMA Cfg');
GO

IF OBJECT_ID('Cfg.Shareholders', 'U') IS NULL
BEGIN
    CREATE TABLE Cfg.Shareholders (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId     INT          NOT NULL,
        FullName      NVARCHAR(200) NOT NULL,
        NationalCode  NVARCHAR(20)  NULL,
        SharePercent  DECIMAL(9,4)  NOT NULL CONSTRAINT DF_Shareholders_Share DEFAULT(0),
        CapitalAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Shareholders_Capital DEFAULT(0),
        Phone         NVARCHAR(30)  NULL,
        Notes         NVARCHAR(500) NULL,
        IsActive      BIT           NOT NULL CONSTRAINT DF_Shareholders_Active DEFAULT(1),
        CreatedAt     DATETIME2     NOT NULL CONSTRAINT DF_Shareholders_Created DEFAULT(SYSDATETIME()),
        UpdatedAt     DATETIME2     NULL
    );
    CREATE INDEX IX_Shareholders_Company ON Cfg.Shareholders(CompanyId);
END
GO
