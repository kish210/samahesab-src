-- =============================================================================
-- ATTP-C1-3 — دستگاهِ تردد + ترددِ خام (پردازشِ خام→روزانه). schema Hrm.
-- جداولِ AuditableEntity ستونِ CreatedAt/UpdatedAt دارند. idempotent؛ GO-split؛ بدونِ USE.
-- =============================================================================
USE SamaHesab;
GO

IF OBJECT_ID('Hrm.Devices', 'U') IS NULL
CREATE TABLE Hrm.Devices (
    Id        int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId int           NOT NULL,
    Name      nvarchar(150) NOT NULL,
    Code      nvarchar(80)  NULL,
    Location  nvarchar(200) NULL,
    IsActive  bit           NOT NULL CONSTRAINT DF_HrmDev_Active  DEFAULT 1,
    CreatedAt datetime      NOT NULL CONSTRAINT DF_HrmDev_Created DEFAULT GETDATE(),
    UpdatedAt datetime      NULL
);
GO

IF OBJECT_ID('Hrm.RawPunches', 'U') IS NULL
CREATE TABLE Hrm.RawPunches (
    Id         int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompanyId  int          NOT NULL,
    EmployeeId int          NOT NULL,
    DeviceId   int          NULL,
    WorkDate   nvarchar(10) NOT NULL,
    PunchTime  time         NOT NULL,
    Processed  bit          NOT NULL CONSTRAINT DF_HrmRaw_Proc    DEFAULT 0,
    CreatedAt  datetime     NOT NULL CONSTRAINT DF_HrmRaw_Created DEFAULT GETDATE(),
    UpdatedAt  datetime     NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RawPunches_Emp_Date' AND object_id=OBJECT_ID('Hrm.RawPunches'))
    CREATE INDEX IX_RawPunches_Emp_Date ON Hrm.RawPunches (CompanyId, EmployeeId, WorkDate);
GO
