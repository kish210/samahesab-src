-- =============================================================================
-- SAMA HESAB ERP — آیتم‌های اخیر/سنجاق‌شده‌ی کاربر (Favorites / Recent / Pinned)
-- سرویس عمومی بهره‌وری: مشتری/کالا/حساب/سندِ پرکاربردِ هر کاربر.
-- idempotent — روی پایگاه‌داده‌ی موجود هم قابل اجراست.
-- =============================================================================
USE SamaHesab;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Cfg')
    EXEC('CREATE SCHEMA Cfg');
GO

IF OBJECT_ID('Cfg.UserItemRefs', 'U') IS NULL
CREATE TABLE Cfg.UserItemRefs (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId  INT NOT NULL,
    UserId     INT NOT NULL,
    EntityType NVARCHAR(40) NOT NULL,   -- Customer / Product / Account / Voucher ...
    EntityId   INT NOT NULL,
    Label      NVARCHAR(250) NOT NULL DEFAULT N'',
    Pinned     BIT NOT NULL DEFAULT 0,
    LastUsedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UseCount   INT NOT NULL DEFAULT 1,
    CreatedAt  DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt  DATETIME2
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserItemRefs_User_Type')
    CREATE INDEX IX_UserItemRefs_User_Type ON Cfg.UserItemRefs(CompanyId, UserId, EntityType, LastUsedAt DESC);
GO

PRINT N'آیتم‌های اخیر/سنجاق‌شده (Cfg.UserItemRefs) با موفقیت ساخته شد.';
GO
