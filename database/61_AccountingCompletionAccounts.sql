-- =============================================================================
-- U-ACCT-1 — تکمیلِ حسابداری: افزودنِ حساب‌هایِ ازقلم‌افتاده در چارتِ حساب‌ها
--   ۱) مالیاتِ بر ارزشِ افزودهٔ خریدِ قابلِ‌کسر (۱-۰۶-۰۰۱) — پیش‌تر مالیاتِ خرید بی‌سروصدا
--      داخلِ بهایِ موجودی folded می‌شد؛ هیچ حسابِ دارایی/طلبِ جداگانه‌ای برایِ آن نبود.
--   ۲) برگشت از فروش (۶-۰۲-۰۰۱) — گروهِ ۶-۰۲ (contra-revenue) از قبل seed شده بود ولی
--      هیچ leafای زیرش نبود؛ برگشت از فروش مستقیماً بدهکارِ حسابِ درآمد می‌شد.
--   ۳) هزینهٔ پورسانتِ فروش (۸-۱۰-۰۰۱) / پورسانتِ پرداختنی به فروشندگان (۳-۰۸-۰۰۱) —
--      پیش‌تر پورسانتِ فروشنده به حساب‌هایِ عمومیِ «حقوق پرسنل»/«پرداختنیِ تأمین‌کننده»
--      می‌رفت (قاطی با ماندهٔ کاملاً نامرتبط).
-- برایِ هر شرکتِ موجود در Cfg.Companies اجرا می‌شود (نه فقط شرکتِ نمونهٔ seedِ اولیه)،
-- idempotent (هر بار اجرا فقط ردیف‌هایِ نبوده را اضافه می‌کند).
-- =============================================================================
USE SamaHesab;
GO

-- گروه‌هایِ سطح۲ِ نو (۳-۰۸/۸-۱۰) — ۱-۰۶ از قبل در ۰۷_DefaultChartOfAccounts.sql هست.
INSERT INTO Acc.Accounts(CompanyId, Code, Name, Level, ParentId, Nature, AccountType, IsLeaf, CreatedAt)
SELECT c.Id, g.Code, g.Name, 2,
       (SELECT Id FROM Acc.Accounts WHERE CompanyId = c.Id AND Code = g.ParentCode),
       g.Nature, g.AccountType, 0, GETDATE()
FROM Cfg.Companies c
CROSS JOIN (VALUES
    (N'3-08', N'پورسانتِ پرداختنی',   N'3', N'بستانکار', N'بدهی'),
    (N'8-10', N'هزینهٔ پورسانتِ فروش', N'8', N'بدهکار',   N'هزینه')
) AS g(Code, Name, ParentCode, Nature, AccountType)
WHERE NOT EXISTS (SELECT 1 FROM Acc.Accounts WHERE CompanyId = c.Id AND Code = g.Code)
  AND EXISTS (SELECT 1 FROM Acc.Accounts WHERE CompanyId = c.Id AND Code = g.ParentCode);
GO

-- حساب‌هایِ سطح۳ (leaf) — قابلِ ثبتِ سند.
INSERT INTO Acc.Accounts(CompanyId, Code, Name, Level, ParentId, Nature, AccountType, IsLeaf, CreatedAt)
SELECT c.Id, a.Code, a.Name, 3,
       (SELECT Id FROM Acc.Accounts WHERE CompanyId = c.Id AND Code = a.ParentCode),
       a.Nature, a.AccountType, 1, GETDATE()
FROM Cfg.Companies c
CROSS JOIN (VALUES
    (N'1-06-001', N'مالیات بر ارزش افزودهٔ خریدِ قابلِ‌کسر', N'1-06', N'بدهکار',   N'دارایی'),
    (N'6-02-001', N'برگشت از فروش',                         N'6-02', N'بدهکار',   N'درآمد'),
    (N'3-08-001', N'پورسانتِ پرداختنی به فروشندگان',       N'3-08', N'بستانکار', N'بدهی'),
    (N'8-10-001', N'هزینهٔ پورسانتِ فروش',                  N'8-10', N'بدهکار',   N'هزینه')
) AS a(Code, Name, ParentCode, Nature, AccountType)
WHERE NOT EXISTS (SELECT 1 FROM Acc.Accounts WHERE CompanyId = c.Id AND Code = a.Code)
  AND EXISTS (SELECT 1 FROM Acc.Accounts WHERE CompanyId = c.Id AND Code = a.ParentCode);
GO
