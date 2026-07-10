-- حذفِ فیچرِ «فاکتورهای تکرارشونده» (به‌درخواستِ کاربر @2026-07-10) — جایگزین: قراردادهایِ خدماتیِ
-- ماژولِ پیمانکاری (Con.ServiceContracts). کدِ C#ِ این فیچر کاملاً حذف شده؛ این اسکریپت جدول‌هایِ
-- دیتابیسِ باقی‌مانده را هم پاک می‌کند (فرزند قبل از والد، به‌خاطرِ FK).
IF OBJECT_ID('Sal.RecurringInvoiceLines', 'U') IS NOT NULL
    DROP TABLE Sal.RecurringInvoiceLines;
GO

IF OBJECT_ID('Sal.RecurringInvoices', 'U') IS NOT NULL
    DROP TABLE Sal.RecurringInvoices;
GO

PRINT N'جدول‌هایِ فاکتورِ تکرارشونده (منسوخ) حذف شدند.';
GO
