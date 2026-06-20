-- =============================================================================
-- 🆘 HC-6b — اتصالِ پشتیبانیِ ریموت (RustDesk): ستون‌های ConnectId/RemoteId/Sync
-- روی جدولِ Sup.RemoteSupportSessions. idempotent؛ توسطِ DatabaseMigratorِ استارت‌آپ اعمال می‌شود.
-- =============================================================================
USE SamaHesab;
GO

IF OBJECT_ID('Sup.RemoteSupportSessions','U') IS NOT NULL
   AND COL_LENGTH('Sup.RemoteSupportSessions','ConnectId') IS NULL
    ALTER TABLE Sup.RemoteSupportSessions ADD ConnectId NVARCHAR(80) NULL;   -- شناسهٔ RustDesk
GO
IF OBJECT_ID('Sup.RemoteSupportSessions','U') IS NOT NULL
   AND COL_LENGTH('Sup.RemoteSupportSessions','RemoteId') IS NULL
    ALTER TABLE Sup.RemoteSupportSessions ADD RemoteId NVARCHAR(60) NULL;    -- شناسهٔ نشست روی vendor
GO
IF OBJECT_ID('Sup.RemoteSupportSessions','U') IS NOT NULL
   AND COL_LENGTH('Sup.RemoteSupportSessions','Sync') IS NULL
    ALTER TABLE Sup.RemoteSupportSessions ADD Sync INT NOT NULL DEFAULT 0;   -- 0=Local 1=Queued 2=Synced
GO

PRINT N'HC-6b: ستون‌های ConnectId/RemoteId/Sync روی Sup.RemoteSupportSessions ensured.';
GO
