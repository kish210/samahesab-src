-- =============================================================================
-- SAMA HESAB ERP — امنیت/RBAC: نقش‌ها و مجوزها (هستهٔ ERP)
-- نقش (Role) + مجوزِ نقش (RolePermission، کدِ «Module.Feature.Action» یا «*») + نقشِ کاربر (UserRole).
-- idempotent — روی پایگاه‌داده‌ی موجود هم قابل اجراست.
-- =============================================================================
USE SamaHesab;
GO

IF OBJECT_ID('Sec.Roles', 'U') IS NULL
CREATE TABLE Sec.Roles (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId  INT NOT NULL,
    Code       NVARCHAR(50) NOT NULL,
    Name       NVARCHAR(100) NOT NULL,
    IsSystem   BIT NOT NULL DEFAULT 0,
    IsActive   BIT NOT NULL DEFAULT 1,
    CreatedAt  DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt  DATETIME2
);
GO
-- سازگاری با drift: اگر Sec.Roles از اسکیمای قدیمیِ 02_CreateTables ساخته شده باشد،
-- ستون‌های CompanyId/UpdatedAt را ندارد → ایندکس و seedِ زیر می‌شکند. idempotent ALTER:
IF OBJECT_ID('Sec.Roles','U') IS NOT NULL
BEGIN
    IF COL_LENGTH('Sec.Roles','CompanyId') IS NULL ALTER TABLE Sec.Roles ADD CompanyId INT NOT NULL DEFAULT 1;
    IF COL_LENGTH('Sec.Roles','UpdatedAt') IS NULL ALTER TABLE Sec.Roles ADD UpdatedAt DATETIME2 NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Roles_Company_Code')
    CREATE UNIQUE INDEX UX_Roles_Company_Code ON Sec.Roles(CompanyId, Code);
GO

-- drop legacy: جدول‌های نگاشتِ RBACِ قدیمی (از 02_CreateTables) مدلِ ناسازگار با EF دارند
-- (RolePermissions: PermissionId+بیت‌فلگ بدونِ Id/PermissionCode · UserRoles: بدونِ Id).
-- چون کدِ فعلی از آن‌ها استفاده نمی‌کند (و seedِ زیر دوباره می‌سازد)، با اسکیمای جدید بازساخته می‌شوند.
IF OBJECT_ID('Sec.RolePermissions','U') IS NOT NULL AND COL_LENGTH('Sec.RolePermissions','PermissionCode') IS NULL
    DROP TABLE Sec.RolePermissions;
GO
IF OBJECT_ID('Sec.UserRoles','U') IS NOT NULL AND COL_LENGTH('Sec.UserRoles','Id') IS NULL
    DROP TABLE Sec.UserRoles;
GO

IF OBJECT_ID('Sec.RolePermissions', 'U') IS NULL
CREATE TABLE Sec.RolePermissions (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    RoleId         INT NOT NULL REFERENCES Sec.Roles(Id) ON DELETE CASCADE,
    PermissionCode NVARCHAR(100) NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_RolePermissions_Role_Code')
    CREATE UNIQUE INDEX UX_RolePermissions_Role_Code ON Sec.RolePermissions(RoleId, PermissionCode);
GO

IF OBJECT_ID('Sec.UserRoles', 'U') IS NULL
CREATE TABLE Sec.UserRoles (
    Id      INT IDENTITY(1,1) PRIMARY KEY,
    UserId  INT NOT NULL,
    RoleId  INT NOT NULL REFERENCES Sec.Roles(Id) ON DELETE CASCADE
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_UserRoles_User_Role')
    CREATE UNIQUE INDEX UX_UserRoles_User_Role ON Sec.UserRoles(UserId, RoleId);
GO

-- ── seed: نقش ADMIN (سیستمی، تمام‌دسترسی «*») برای شرکت ۱ + تخصیص به کاربر admin ──
DECLARE @companyId INT = 1;

IF NOT EXISTS (SELECT 1 FROM Sec.Roles WHERE CompanyId = @companyId AND Code = N'ADMIN')
    INSERT INTO Sec.Roles (CompanyId, Code, Name, IsSystem, IsActive)
    VALUES (@companyId, N'ADMIN', N'مدیر سیستم', 1, 1);
GO

DECLARE @companyId INT = 1;
DECLARE @adminRoleId INT = (SELECT Id FROM Sec.Roles WHERE CompanyId = @companyId AND Code = N'ADMIN');

IF @adminRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Sec.RolePermissions WHERE RoleId = @adminRoleId AND PermissionCode = N'*')
    INSERT INTO Sec.RolePermissions (RoleId, PermissionCode) VALUES (@adminRoleId, N'*');

DECLARE @adminUserId INT = (SELECT TOP 1 Id FROM Sec.Users WHERE CompanyId = @companyId AND Username = N'admin');
IF @adminUserId IS NOT NULL AND @adminRoleId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Sec.UserRoles WHERE UserId = @adminUserId AND RoleId = @adminRoleId)
    INSERT INTO Sec.UserRoles (UserId, RoleId) VALUES (@adminUserId, @adminRoleId);
GO

PRINT N'امنیت/RBAC (Sec.Roles/RolePermissions/UserRoles) با موفقیت ساخته و seed شد.';
GO
