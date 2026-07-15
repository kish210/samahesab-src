-- =============================================================================
-- U-CONSIGN-2 — حسابِ «کالای امانی نزدِ دیگران» (Consignment-Out Inventory)
--   کالایِ ارسالی به‌صورتِ کنسینمنت هنوز مالِ فرستنده است (مالکیت منتقل نشده) — پس فروش/درآمد/
--   COGS در لحظهٔ ارسال شناسایی نمی‌شود، فقط یک reclassificationِ درونِ داراییِ موجودی: از
--   «موجودی کالا - انبار مرکزی» (۱-۰۵-۰۰۱) به این حسابِ جدید، به بهایِ تمام‌شده (نه قیمتِ فروش).
--   زیرِ همان گروهِ ۱-۰۵ (موجودی کالا) — چون از نظرِ ماهیت هنوز موجودیِ شرکت است، فقط جای فیزیکی‌اش
--   جای دیگری‌ست؛ هم‌الگو با ۱-۰۵-۰۰۲ (موجودی کالا در راه) که از قبل seed شده بود.
-- =============================================================================
USE SamaHesab;
GO

INSERT INTO Acc.Accounts(CompanyId, Code, Name, Level, ParentId, Nature, AccountType, IsLeaf, CreatedAt)
SELECT c.Id, a.Code, a.Name, 3,
       (SELECT Id FROM Acc.Accounts WHERE CompanyId = c.Id AND Code = a.ParentCode),
       a.Nature, a.AccountType, 1, GETDATE()
FROM Cfg.Companies c
CROSS JOIN (VALUES
    (N'1-05-003', N'کالای امانی نزدِ دیگران (کنسینمنت)', N'1-05', N'بدهکار', N'دارایی')
) AS a(Code, Name, ParentCode, Nature, AccountType)
WHERE NOT EXISTS (SELECT 1 FROM Acc.Accounts WHERE CompanyId = c.Id AND Code = a.Code)
  AND EXISTS (SELECT 1 FROM Acc.Accounts WHERE CompanyId = c.Id AND Code = a.ParentCode);
GO
