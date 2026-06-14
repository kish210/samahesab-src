-- =============================================================================
-- MB-1 گام۴: حساب جاریِ فی‌مابینِ شعب (inter-branch clearing account)
-- واسطِ سندِ تسویهٔ بین‌شعبه (CreateInterBranchTransferCommand): خالصِ این حساب
-- روی کلِ شرکت صفر می‌ماند (طلبِ یک شعبه = بدهیِ شعبهٔ دیگر).
-- idempotent: برای هر شرکتی که حساب کلِ «1-07 سایر دارایی‌های جاری» را دارد ولی
-- معینِ «1-07-001» را ندارد، آن را به‌عنوان حسابِ برگ (IsLeaf) اضافه می‌کند.
-- =============================================================================
USE SamaHesab;
GO

INSERT INTO Acc.Accounts (CompanyId, Code, Name, Level, ParentId, Nature, AccountType, IsLeaf, IsSystem, IsActive, CreatedAt)
SELECT  p.CompanyId, N'1-07-001', N'حساب جاریِ فی‌مابینِ شعب', 3, p.Id, N'بدهکار', N'دارایی', 1, 1, 1, GETDATE()
FROM    Acc.Accounts p
WHERE   p.Code = N'1-07'
  AND   NOT EXISTS (
            SELECT 1 FROM Acc.Accounts c
            WHERE c.CompanyId = p.CompanyId AND c.Code = N'1-07-001');
GO

PRINT N'MB-1 گام۴: حساب فی‌مابینِ شعب (1-07-001) ensured.';
GO
