-- =============================================================================
-- SAMA HESAB ERP - سامانه جامع سما حساب
-- Database Creation Script
-- Version: 1.0.0
-- =============================================================================

USE master;
GO

IF EXISTS (SELECT name FROM sys.databases WHERE name = N'SamaHesab')
BEGIN
    ALTER DATABASE SamaHesab SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE SamaHesab;
END
GO

CREATE DATABASE SamaHesab
ON PRIMARY
(
    NAME = N'SamaHesab',
    FILENAME = N'/var/opt/mssql/data/SamaHesab.mdf',
    SIZE = 512MB,
    MAXSIZE = UNLIMITED,
    FILEGROWTH = 256MB
)
LOG ON
(
    NAME = N'SamaHesab_log',
    FILENAME = N'/var/opt/mssql/data/SamaHesab_log.ldf',
    SIZE = 128MB,
    MAXSIZE = 2048MB,
    FILEGROWTH = 64MB
);
GO

ALTER DATABASE SamaHesab SET RECOVERY FULL;
ALTER DATABASE SamaHesab SET AUTO_SHRINK OFF;
ALTER DATABASE SamaHesab SET AUTO_UPDATE_STATISTICS ON;
ALTER DATABASE SamaHesab SET ALLOW_SNAPSHOT_ISOLATION ON;
ALTER DATABASE SamaHesab SET READ_COMMITTED_SNAPSHOT ON;
ALTER DATABASE SamaHesab COLLATE Persian_100_CI_AI;
GO

USE SamaHesab;
GO

-- =============================================================================
-- SCHEMAS
-- =============================================================================
CREATE SCHEMA Acc;   -- Accounting
GO
CREATE SCHEMA Inv;   -- Inventory
GO
CREATE SCHEMA Sal;   -- Sales
GO
CREATE SCHEMA Pur;   -- Purchase
GO
CREATE SCHEMA Pos;   -- Point of Sale
GO
CREATE SCHEMA Crm;   -- CRM
GO
CREATE SCHEMA Hrm;   -- HRM
GO
CREATE SCHEMA Cfg;   -- Configuration/Settings
GO
CREATE SCHEMA Sec;   -- Security
GO
