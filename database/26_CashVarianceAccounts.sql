-- =============================================================================
-- T18: حساب‌های «کسری/اضافهٔ صندوق» برای سندِ خودکارِ بستنِ شیفت (Z-report).
-- کسری (هزینه): 8-11-001 زیرِ «سایر هزینه‌های عملیاتی» (8-11).
-- اضافه (درآمد): 6-03-001 زیرِ «سایر درآمدهای عملیاتی» (6-03).
-- idempotent: برای هر شرکتی که حسابِ کلِ والد را دارد و معینِ مربوطه را ندارد.
-- (مهاجرتِ افزایشی؛ توسطِ DatabaseMigratorِ استارت‌آپ هم خودکار اعمال می‌شود.)
-- =============================================================================
USE SamaHesab;
GO

INSERT INTO Acc.Accounts (CompanyId, Code, Name, Level, ParentId, Nature, AccountType, IsLeaf, IsSystem, IsActive, CreatedAt)
SELECT  p.CompanyId, N'8-11-001', N'کسریِ صندوق', 3, p.Id, N'بدهکار', N'هزینه', 1, 1, 1, GETDATE()
FROM    Acc.Accounts p
WHERE   p.Code = N'8-11'
  AND   NOT EXISTS (SELECT 1 FROM Acc.Accounts c WHERE c.CompanyId = p.CompanyId AND c.Code = N'8-11-001');
GO

INSERT INTO Acc.Accounts (CompanyId, Code, Name, Level, ParentId, Nature, AccountType, IsLeaf, IsSystem, IsActive, CreatedAt)
SELECT  p.CompanyId, N'6-03-001', N'اضافاتِ صندوق', 3, p.Id, N'بستانکار', N'درآمد', 1, 1, 1, GETDATE()
FROM    Acc.Accounts p
WHERE   p.Code = N'6-03'
  AND   NOT EXISTS (SELECT 1 FROM Acc.Accounts c WHERE c.CompanyId = p.CompanyId AND c.Code = N'6-03-001');
GO

PRINT N'T18: حساب‌های کسری/اضافهٔ صندوق (8-11-001 / 6-03-001) ensured.';
GO
