-- =============================================================================
-- SAMA HESAB ERP - نمودار حساب‌های پیش‌فرض ایران
-- Iranian Standard Chart of Accounts
-- =============================================================================
USE SamaHesab;
GO

-- Insert default company if not exists
IF NOT EXISTS (SELECT 1 FROM Cfg.Companies WHERE Code = N'001')
BEGIN
    INSERT INTO Cfg.Companies(Code, Name, FiscalYearStart, FiscalYearEnd, Currency)
    VALUES(N'001', N'شرکت نمونه', N'1403/01/01', N'1403/12/29', N'ریال');
END

DECLARE @CompanyId INT = (SELECT TOP 1 Id FROM Cfg.Companies WHERE Code = N'001');
DECLARE @BranchId INT;

IF NOT EXISTS (SELECT 1 FROM Cfg.Branches WHERE CompanyId = @CompanyId AND Code = N'HQ')
BEGIN
    INSERT INTO Cfg.Branches(CompanyId, Code, Name, IsHQ)
    VALUES(@CompanyId, N'HQ', N'دفتر مرکزی', 1);
END

SET @BranchId = (SELECT TOP 1 Id FROM Cfg.Branches WHERE CompanyId = @CompanyId AND Code = N'HQ');

-- Fiscal Year
IF NOT EXISTS (SELECT 1 FROM Cfg.FiscalYears WHERE CompanyId = @CompanyId)
BEGIN
    INSERT INTO Cfg.FiscalYears(CompanyId, Title, StartDate, EndDate, IsClosed, IsActive)
    VALUES(@CompanyId, N'سال ۱۴۰۳', N'1403/01/01', N'1403/12/29', 0, 1);
END

-- =============================================================================
-- CHART OF ACCOUNTS - نمودار حساب‌های استاندارد ایران
-- =============================================================================

-- Helper procedure to insert accounts
DECLARE @accounts TABLE (
    Code NVARCHAR(40), Name NVARCHAR(400), Level INT,
    ParentCode NVARCHAR(40), Nature NVARCHAR(20), AccountType NVARCHAR(100)
);

-- LEVEL 1: Group Accounts (گروه‌ها)
INSERT INTO @accounts(Code,Name,Level,ParentCode,Nature,AccountType) VALUES
('1',   N'دارایی‌های جاری',           1, NULL, N'بدهکار',   N'دارایی'),
('2',   N'دارایی‌های ثابت',           1, NULL, N'بدهکار',   N'دارایی'),
('3',   N'بدهی‌های جاری',             1, NULL, N'بستانکار', N'بدهی'),
('4',   N'بدهی‌های بلندمدت',          1, NULL, N'بستانکار', N'بدهی'),
('5',   N'حقوق صاحبان سهام',          1, NULL, N'بستانکار', N'سرمایه'),
('6',   N'درآمدها',                    1, NULL, N'بستانکار', N'درآمد'),
('7',   N'بهای تمام شده',             1, NULL, N'بدهکار',   N'هزینه'),
('8',   N'هزینه‌های عملیاتی',          1, NULL, N'بدهکار',   N'هزینه'),
('9',   N'سایر درآمدها و هزینه‌ها',    1, NULL, N'بدهکار',   N'هزینه');

-- LEVEL 2: General Accounts (حساب‌های کل)
INSERT INTO @accounts(Code,Name,Level,ParentCode,Nature,AccountType) VALUES
-- دارایی‌های جاری
('1-01', N'وجوه نقد و معادل‌های نقد',      2,'1',N'بدهکار',N'دارایی'),
('1-02', N'سرمایه‌گذاری‌های کوتاه‌مدت',   2,'1',N'بدهکار',N'دارایی'),
('1-03', N'حساب‌های دریافتنی',              2,'1',N'بدهکار',N'دارایی'),
('1-04', N'اسناد دریافتنی',                 2,'1',N'بدهکار',N'دارایی'),
('1-05', N'موجودی کالا',                    2,'1',N'بدهکار',N'دارایی'),
('1-06', N'پیش‌پرداخت‌ها',                 2,'1',N'بدهکار',N'دارایی'),
('1-07', N'سایر دارایی‌های جاری',           2,'1',N'بدهکار',N'دارایی'),
-- دارایی‌های ثابت
('2-01', N'زمین',                            2,'2',N'بدهکار',N'دارایی'),
('2-02', N'ساختمان',                         2,'2',N'بدهکار',N'دارایی'),
('2-03', N'ماشین‌آلات و تجهیزات',           2,'2',N'بدهکار',N'دارایی'),
('2-04', N'وسایل نقلیه',                    2,'2',N'بدهکار',N'دارایی'),
('2-05', N'اثاثه و لوازم اداری',            2,'2',N'بدهکار',N'دارایی'),
('2-06', N'استهلاک انباشته دارایی‌های ثابت',2,'2',N'بستانکار',N'دارایی'),
('2-07', N'دارایی‌های نامشهود',             2,'2',N'بدهکار',N'دارایی'),
-- بدهی‌های جاری
('3-01', N'حساب‌های پرداختنی',              2,'3',N'بستانکار',N'بدهی'),
('3-02', N'اسناد پرداختنی',                 2,'3',N'بستانکار',N'بدهی'),
('3-03', N'پیش‌دریافت‌ها',                  2,'3',N'بستانکار',N'بدهی'),
('3-04', N'مالیات پرداختنی',                2,'3',N'بستانکار',N'بدهی'),
('3-05', N'حقوق پرداختنی',                  2,'3',N'بستانکار',N'بدهی'),
('3-06', N'سود سهام پرداختنی',              2,'3',N'بستانکار',N'بدهی'),
('3-07', N'تسهیلات کوتاه‌مدت',              2,'3',N'بستانکار',N'بدهی'),
-- بدهی‌های بلندمدت
('4-01', N'تسهیلات بلندمدت',               2,'4',N'بستانکار',N'بدهی'),
('4-02', N'ذخیره مزایای پایان خدمت',       2,'4',N'بستانکار',N'بدهی'),
-- حقوق صاحبان سهام
('5-01', N'سرمایه',                          2,'5',N'بستانکار',N'سرمایه'),
('5-02', N'اندوخته قانونی',                 2,'5',N'بستانکار',N'سرمایه'),
('5-03', N'سود (زیان) انباشته',             2,'5',N'بستانکار',N'سرمایه'),
('5-04', N'سود (زیان) جاری',               2,'5',N'بستانکار',N'سرمایه'),
-- درآمدها
('6-01', N'درآمد فروش',                     2,'6',N'بستانکار',N'درآمد'),
('6-02', N'برگشت از فروش و تخفیفات',       2,'6',N'بدهکار',  N'درآمد'),
('6-03', N'سایر درآمدهای عملیاتی',          2,'6',N'بستانکار',N'درآمد'),
-- بهای تمام شده
('7-01', N'بهای تمام شده کالای فروخته شده',2,'7',N'بدهکار',N'هزینه'),
-- هزینه‌های عملیاتی
('8-01', N'حقوق و دستمزد',                  2,'8',N'بدهکار',N'هزینه'),
('8-02', N'اجاره',                           2,'8',N'بدهکار',N'هزینه'),
('8-03', N'استهلاک',                         2,'8',N'بدهکار',N'هزینه'),
('8-04', N'تعمیر و نگهداری',               2,'8',N'بدهکار',N'هزینه'),
('8-05', N'آب، برق و گاز',                  2,'8',N'بدهکار',N'هزینه'),
('8-06', N'بیمه',                            2,'8',N'بدهکار',N'هزینه'),
('8-07', N'حمل و نقل',                      2,'8',N'بدهکار',N'هزینه'),
('8-08', N'تبلیغات و بازاریابی',            2,'8',N'بدهکار',N'هزینه'),
('8-09', N'مکاتبات و ارتباطات',             2,'8',N'بدهکار',N'هزینه'),
('8-10', N'هزینه‌های مالی',                 2,'8',N'بدهکار',N'هزینه'),
('8-11', N'سایر هزینه‌های عملیاتی',         2,'8',N'بدهکار',N'هزینه');

-- LEVEL 3: Subsidiary Accounts (معین)
INSERT INTO @accounts(Code,Name,Level,ParentCode,Nature,AccountType) VALUES
-- وجوه نقد
('1-01-001', N'صندوق',                       3,'1-01',N'بدهکار',N'دارایی'),
('1-01-002', N'تنخواه‌گردان',                3,'1-01',N'بدهکار',N'دارایی'),
-- بانک‌ها
('1-01-003', N'بانک ملت',                    3,'1-01',N'بدهکار',N'دارایی'),
('1-01-004', N'بانک ملی',                    3,'1-01',N'بدهکار',N'دارایی'),
('1-01-005', N'بانک تجارت',                  3,'1-01',N'بدهکار',N'دارایی'),
('1-01-006', N'بانک صادرات',                 3,'1-01',N'بدهکار',N'دارایی'),
('1-01-007', N'بانک رفاه',                   3,'1-01',N'بدهکار',N'دارایی'),
-- حساب‌های دریافتنی
('1-03-001', N'حساب‌های دریافتنی - مشتریان', 3,'1-03',N'بدهکار',N'دارایی'),
('1-03-002', N'حساب‌های دریافتنی - سایر',    3,'1-03',N'بدهکار',N'دارایی'),
-- اسناد دریافتنی
('1-04-001', N'چک‌های دریافتنی در جریان',    3,'1-04',N'بدهکار',N'دارایی'),
('1-04-002', N'چک‌های دریافتنی نزد بانک',    3,'1-04',N'بدهکار',N'دارایی'),
-- موجودی کالا
('1-05-001', N'موجودی کالا - انبار مرکزی',   3,'1-05',N'بدهکار',N'دارایی'),
('1-05-002', N'موجودی کالا در راه',          3,'1-05',N'بدهکار',N'دارایی'),
-- حساب‌های پرداختنی
('3-01-001', N'حساب‌های پرداختنی - تأمین‌کنندگان',3,'3-01',N'بستانکار',N'بدهی'),
('3-01-002', N'حساب‌های پرداختنی - سایر',    3,'3-01',N'بستانکار',N'بدهی'),
-- اسناد پرداختنی
('3-02-001', N'اسناد پرداختنی - چک',         3,'3-02',N'بستانکار',N'بدهی'),
-- مالیات
('3-04-001', N'مالیات بر ارزش افزوده پرداختنی',3,'3-04',N'بستانکار',N'بدهی'),
('3-04-002', N'مالیات بر درآمد پرداختنی',    3,'3-04',N'بستانکار',N'بدهی'),
-- درآمد فروش
('6-01-001', N'درآمد فروش کالا',             3,'6-01',N'بستانکار',N'درآمد'),
('6-01-002', N'درآمد فروش خدمات',            3,'6-01',N'بستانکار',N'درآمد'),
-- بهای تمام شده
('7-01-001', N'بهای تمام شده کالای فروخته شده',3,'7-01',N'بدهکار',N'هزینه'),
-- حقوق
('8-01-001', N'حقوق و مزایای پرسنل',         3,'8-01',N'بدهکار',N'هزینه'),
('8-01-002', N'حق بیمه سهم کارفرما',         3,'8-01',N'بدهکار',N'هزینه'),
('8-01-003', N'عیدی و پاداش',               3,'8-01',N'بدهکار',N'هزینه');

-- Now insert all accounts
INSERT INTO Acc.Accounts(CompanyId, Code, Name, Level, ParentId, Nature, AccountType, IsLeaf, CreatedAt)
SELECT
    @CompanyId,
    a.Code,
    a.Name,
    a.Level,
    (SELECT Id FROM Acc.Accounts WHERE CompanyId = @CompanyId AND Code = a.ParentCode),
    a.Nature,
    a.AccountType,
    CASE WHEN a.Level >= 3 THEN 1 ELSE 0 END,
    GETDATE()
FROM @accounts a
WHERE NOT EXISTS (SELECT 1 FROM Acc.Accounts WHERE CompanyId = @CompanyId AND Code = a.Code)
ORDER BY a.Level, a.Code;

-- Update AppSettings with default account IDs
DECLARE @CashAccountId INT = (SELECT Id FROM Acc.Accounts WHERE CompanyId = @CompanyId AND Code = N'1-01-001');
DECLARE @SalesAccountId INT = (SELECT Id FROM Acc.Accounts WHERE CompanyId = @CompanyId AND Code = N'6-01-001');
DECLARE @ARAccountId INT = (SELECT Id FROM Acc.Accounts WHERE CompanyId = @CompanyId AND Code = N'1-03-001');
DECLARE @APAccountId INT = (SELECT Id FROM Acc.Accounts WHERE CompanyId = @CompanyId AND Code = N'3-01-001');
DECLARE @CogsAccountId INT = (SELECT Id FROM Acc.Accounts WHERE CompanyId = @CompanyId AND Code = N'7-01-001');
DECLARE @InventoryAccountId INT = (SELECT Id FROM Acc.Accounts WHERE CompanyId = @CompanyId AND Code = N'1-05-001');

-- Store default account IDs
MERGE Cfg.AppSettings AS target
USING (VALUES
    (@CompanyId, N'DefaultCashAccountId',       CAST(@CashAccountId AS NVARCHAR)),
    (@CompanyId, N'DefaultSalesAccountId',      CAST(@SalesAccountId AS NVARCHAR)),
    (@CompanyId, N'DefaultARAccountId',         CAST(@ARAccountId AS NVARCHAR)),
    (@CompanyId, N'DefaultAPAccountId',         CAST(@APAccountId AS NVARCHAR)),
    (@CompanyId, N'DefaultCogsAccountId',       CAST(@CogsAccountId AS NVARCHAR)),
    (@CompanyId, N'DefaultInventoryAccountId',  CAST(@InventoryAccountId AS NVARCHAR))
) AS source(CompanyId, [Key], [Value])
ON target.CompanyId = source.CompanyId AND target.[Key] = source.[Key]
WHEN NOT MATCHED THEN INSERT(CompanyId, [Key], [Value]) VALUES(source.CompanyId, source.[Key], source.[Value]);

-- Default Warehouse
IF NOT EXISTS (SELECT 1 FROM Inv.Warehouses WHERE CompanyId = @CompanyId)
BEGIN
    INSERT INTO Inv.Warehouses(CompanyId, BranchId, Code, Name, IsDefault)
    VALUES(@CompanyId, @BranchId, N'W001', N'انبار مرکزی', 1);
END

-- Default Admin User (password: admin123)
-- ⚠️ باگِ واقعیِ رفع‌شده (@2026-07-22): هش/ساتِ پیشین با SHA256(password+salt) ساده محاسبه شده
-- بودند، درحالی‌که برنامه واقعاً با PasswordHasher.Verify (PBKDF2-SHA256، ۱۰۰هزار تکرار، کلیدِ
-- ۳۲بایتی — src/SamaHesab.Application/Common/Security/PasswordHasher.cs) تأیید می‌کند. یعنی روی
-- هر نصبِ تازه، لاگینِ admin/admin123 هرگز کار نمی‌کرد. مقادیرِ زیر با همان الگوریتمِ واقعی برای
-- رمزِ admin123 از نو محاسبه شده‌اند.
IF NOT EXISTS (SELECT 1 FROM Sec.Users WHERE CompanyId = @CompanyId AND Username = N'admin')
BEGIN
    DECLARE @Salt NVARCHAR(100) = N'TfuKbvTVS2f47DsTSl9uAA==';
    -- PBKDF2-SHA256(password='admin123', iterations=100000, keySize=32) — سازگار با PasswordHasher.Verify
    DECLARE @Hash NVARCHAR(256) = N'dINuLAPR1tjGi3fvfMn5v+QYO9HXGyEaxYut/aCmkd8=';

    INSERT INTO Sec.Users(CompanyId, BranchId, Username, PasswordHash, PasswordSalt, FullName, IsActive)
    VALUES(@CompanyId, @BranchId, N'admin', @Hash, @Salt, N'مدیر سیستم', 1);

    DECLARE @AdminUserId INT = SCOPE_IDENTITY();
    DECLARE @AdminRoleId INT = (SELECT Id FROM Sec.Roles WHERE Code = N'ADMIN');

    INSERT INTO Sec.UserRoles(UserId, RoleId) VALUES(@AdminUserId, @AdminRoleId);
END

PRINT N'نمودار حساب‌های پیش‌فرض با موفقیت ایجاد شد.';
GO
