-- =============================================================================
-- رفعِ باگِ encoding — نامِ ۲ قالبِ seedِ اولیه (DT-2) به‌خاطرِ نبودِ پیشوندِ N
-- در اسکریپتِ 28 به‌صورتِ mojibake ذخیره شده بود. این اسکریپت نام‌های صحیح را
-- روی DBهای موجود بازنویسی می‌کند. اسکریپتِ 28 در سورس هم اصلاح شد (N'...').
-- idempotent: امن برای اجرای مکرر (توسطِ migration-runner).
-- =============================================================================
USE SamaHesab;
GO

IF OBJECT_ID('Cfg.DocumentTemplates') IS NOT NULL
BEGIN
    -- قالبِ سیستمیِ A4 (فاکتور رسمی)
    UPDATE Cfg.DocumentTemplates
       SET Name = N'فاکتور رسمی (A4)'
     WHERE DocumentType = 'SalesInvoice' AND PaperSize = 'A4P' AND IsSystem = 1
       AND Name <> N'فاکتور رسمی (A4)';

    -- قالبِ سیستمیِ حرارتی ۸۰م‌م
    UPDATE Cfg.DocumentTemplates
       SET Name = N'رسیدِ حرارتی (۸۰م م)'
     WHERE DocumentType = 'SalesInvoice' AND PaperSize = 'Thermal80' AND IsSystem = 1
       AND Name <> N'رسیدِ حرارتی (۸۰م م)';
END
GO
