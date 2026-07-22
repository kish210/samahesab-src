-- =============================================================================
-- U-ATT-ZK — افزودنِ اطلاعاتِ اتصالِ شبکه‌ایِ دستگاهِ زدکتکو به Hrm.Devices.
-- idempotent؛ GO-split؛ بدونِ USE.
-- =============================================================================
USE SamaHesab;
GO

IF COL_LENGTH('Hrm.Devices', 'IpAddress') IS NULL
    ALTER TABLE Hrm.Devices ADD IpAddress nvarchar(50) NULL;
GO
IF COL_LENGTH('Hrm.Devices', 'Port') IS NULL
    ALTER TABLE Hrm.Devices ADD Port int NOT NULL CONSTRAINT DF_HrmDev_Port DEFAULT 4370;
GO
IF COL_LENGTH('Hrm.Devices', 'CommKey') IS NULL
    ALTER TABLE Hrm.Devices ADD CommKey nvarchar(50) NULL;
GO
