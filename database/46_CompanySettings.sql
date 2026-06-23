-- =============================================================================
-- CR-X8 — تنظیماتِ شرکتیِ کلید-مقدار در DB (همگامیِ چندایستگاهی). schema Cfg.
-- AuditableEntity → ستون‌های CreatedAt/UpdatedAt. idempotent؛ GO-split؛ بدونِ USE.
-- =============================================================================
USE SamaHesab;
GO

IF SCHEMA_ID('Cfg') IS NULL EXEC('CREATE SCHEMA Cfg');
GO

IF OBJECT_ID('Cfg.CompanySettings', 'U') IS NULL
CREATE TABLE Cfg.CompanySettings (
    Id        int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId int            NOT NULL,
    [Key]     nvarchar(100)  NOT NULL,
    Value     nvarchar(max)  NULL,
    CreatedAt datetime       NOT NULL CONSTRAINT DF_CompanySettings_Created DEFAULT GETDATE(),
    UpdatedAt datetime       NULL,
    CONSTRAINT UQ_CompanySettings_Company_Key UNIQUE (CompanyId, [Key])
);
GO
