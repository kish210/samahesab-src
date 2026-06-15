-- =============================================================================
-- M7 فاز۲: حساب‌های معینِ پرداختِ حقوق برای سندِ خودکارِ حقوق.
-- 3-05-001 حقوق پرداختنی · 3-05-002 بیمهٔ تأمین اجتماعی پرداختنی (زیرِ 3-05).
-- (هزینهٔ حقوق = 8-01-001 و مالیات = 3-04-002 از قبل در نمودار هست.)
-- idempotent؛ توسطِ DatabaseMigratorِ استارت‌آپ هم خودکار اعمال می‌شود.
-- =============================================================================
USE SamaHesab;
GO

INSERT INTO Acc.Accounts (CompanyId, Code, Name, Level, ParentId, Nature, AccountType, IsLeaf, IsSystem, IsActive, CreatedAt)
SELECT  p.CompanyId, N'3-05-001', N'حقوق پرداختنی', 3, p.Id, N'بستانکار', N'بدهی', 1, 1, 1, GETDATE()
FROM    Acc.Accounts p
WHERE   p.Code = N'3-05'
  AND   NOT EXISTS (SELECT 1 FROM Acc.Accounts c WHERE c.CompanyId = p.CompanyId AND c.Code = N'3-05-001');
GO

INSERT INTO Acc.Accounts (CompanyId, Code, Name, Level, ParentId, Nature, AccountType, IsLeaf, IsSystem, IsActive, CreatedAt)
SELECT  p.CompanyId, N'3-05-002', N'بیمهٔ تأمین اجتماعی پرداختنی', 3, p.Id, N'بستانکار', N'بدهی', 1, 1, 1, GETDATE()
FROM    Acc.Accounts p
WHERE   p.Code = N'3-05'
  AND   NOT EXISTS (SELECT 1 FROM Acc.Accounts c WHERE c.CompanyId = p.CompanyId AND c.Code = N'3-05-002');
GO

PRINT N'M7: حساب‌های پرداختِ حقوق (3-05-001 / 3-05-002) ensured.';
GO
