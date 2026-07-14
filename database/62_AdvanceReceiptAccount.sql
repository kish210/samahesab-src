-- =============================================================================
-- U-ACCT-1.3 — حساب‌هایِ پیش‌دریافت/پیش‌پرداختِ ناشی از مازادِ دریافت/پرداخت
--   ۱) ۳-۰۳-۰۰۱ — گروهِ ازپیش‌seedشدهٔ ۳-۰۳ (پیش‌دریافت‌ها) هیچ leafای نداشت. وقتی دریافتیِ
--      مشتری از مجموعِ ماندهٔ فاکتورهایِ بازش بیشتر باشد، مازاد به‌جایِ بی‌سروصدا دورریختن
--      (پیش‌تر) این‌جا (نه ۱-۰۳-۰۰۱ حساب‌های دریافتنی) بستانکار می‌شود.
--   ۲) ۱-۰۶-۰۰۲ — همان، برایِ سمتِ پرداخت به تأمین‌کننده (پیش‌پرداخت، زیرِ همان گروهِ
--      پیش‌پرداخت‌ها که ۱-۰۶-۰۰۱ِ مالیاتِ قابلِ‌کسر هم آن‌جاست).
-- طبقه‌بندیِ درستِ صورت‌هایِ مالی: مازادِ دریافت/پرداخت یک بدهی/دارایـیِ جداگانه است، نه صرفاً
-- کاهشِ حسابِ دریافتنی/پرداختنی.
-- =============================================================================
USE SamaHesab;
GO

INSERT INTO Acc.Accounts(CompanyId, Code, Name, Level, ParentId, Nature, AccountType, IsLeaf, CreatedAt)
SELECT c.Id, a.Code, a.Name, 3,
       (SELECT Id FROM Acc.Accounts WHERE CompanyId = c.Id AND Code = a.ParentCode),
       a.Nature, a.AccountType, 1, GETDATE()
FROM Cfg.Companies c
CROSS JOIN (VALUES
    (N'3-03-001', N'پیش‌دریافت از مشتریان',        N'3-03', N'بستانکار', N'بدهی'),
    (N'1-06-002', N'پیش‌پرداخت به تأمین‌کنندگان', N'1-06', N'بدهکار',   N'دارایی')
) AS a(Code, Name, ParentCode, Nature, AccountType)
WHERE NOT EXISTS (SELECT 1 FROM Acc.Accounts WHERE CompanyId = c.Id AND Code = a.Code)
  AND EXISTS (SELECT 1 FROM Acc.Accounts WHERE CompanyId = c.Id AND Code = a.ParentCode);
GO
