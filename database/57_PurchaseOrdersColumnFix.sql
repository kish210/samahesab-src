-- =============================================================================
-- SAMA HESAB ERP — رفعِ عقب‌ماندگیِ اسکیمای Pur.PurchaseOrders روی DBهای قدیمی
-- باگِ کشف‌شده @2026-07-08: صفحهٔ «سفارش‌های خرید» با خطایِ
-- «Invalid column name 'Description/Source/StatusCode/Total/UpdatedAt'» کرش می‌کرد.
-- ریشه: __AppliedScripts نامِ اسکریپت را ردیابی می‌کند نه محتوایش — 15_PurchaseOrders.sql
-- قبلاً (با نسخه‌ی قدیمی‌ترِ بدونِ این ستون‌ها) روی این DB اجرا و «applied» ثبت شده بود،
-- پس وقتی بعداً ALTERهای idempotent به همان فایل اضافه شدند، دیگر هرگز دوباره اجرا نشدند.
-- این فایل با نامِ نو، همان ALTERها را قطعاً یک‌بار روی این DBها اجرا می‌کند. ستون‌های
-- قدیمیِ Status/GrandTotal/Notes/ExpectedDate/CreatedByUserId/FiscalYearId دست‌نخورده
-- می‌مانند (فقط افزودن، نه حذف/تغییرِ نام) — بدونِ خطرِ از دست رفتنِ داده.
-- idempotent — روی پایگاه‌داده‌ی موجود هم قابل اجراست.
-- =============================================================================
USE SamaHesab;
GO

IF COL_LENGTH('Pur.PurchaseOrders', 'StatusCode') IS NULL
    ALTER TABLE Pur.PurchaseOrders ADD StatusCode NVARCHAR(20) NOT NULL CONSTRAINT DF_PO_StatusCode_v2 DEFAULT N'پیش‌نویس';
IF COL_LENGTH('Pur.PurchaseOrders', 'Source') IS NULL
    ALTER TABLE Pur.PurchaseOrders ADD Source NVARCHAR(20) NOT NULL CONSTRAINT DF_PO_Source_v2 DEFAULT N'دستی';
IF COL_LENGTH('Pur.PurchaseOrders', 'Description') IS NULL
    ALTER TABLE Pur.PurchaseOrders ADD Description NVARCHAR(500) NULL;
IF COL_LENGTH('Pur.PurchaseOrders', 'Total') IS NULL
    ALTER TABLE Pur.PurchaseOrders ADD Total DECIMAL(18,2) NOT NULL CONSTRAINT DF_PO_Total_v2 DEFAULT 0;
IF COL_LENGTH('Pur.PurchaseOrders', 'CreatedAt') IS NULL
    ALTER TABLE Pur.PurchaseOrders ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PO_CreatedAt_v2 DEFAULT GETDATE();
IF COL_LENGTH('Pur.PurchaseOrders', 'UpdatedAt') IS NULL
    ALTER TABLE Pur.PurchaseOrders ADD UpdatedAt DATETIME2 NULL;
IF COL_LENGTH('Pur.PurchaseOrders', 'SupplierId') IS NULL
    ALTER TABLE Pur.PurchaseOrders ADD SupplierId INT NULL;
GO

-- روی اسکیمایِ قدیمی، GrandTotal/Status ممکن است دادهٔ واقعی داشته باشند؛ اگر Total/StatusCode
-- تازه اضافه و صفر/پیش‌فرض‌اند، مقدارِ قدیمی را به ستونِ نو کپی می‌کنیم تا دادهٔ کاربر گم نشود.
IF COL_LENGTH('Pur.PurchaseOrders', 'GrandTotal') IS NOT NULL AND COL_LENGTH('Pur.PurchaseOrders', 'Total') IS NOT NULL
    UPDATE Pur.PurchaseOrders SET Total = GrandTotal WHERE Total = 0 AND GrandTotal <> 0;
IF COL_LENGTH('Pur.PurchaseOrders', 'Status') IS NOT NULL AND COL_LENGTH('Pur.PurchaseOrders', 'StatusCode') IS NOT NULL
    UPDATE Pur.PurchaseOrders SET StatusCode = Status WHERE StatusCode = N'پیش‌نویس' AND Status IS NOT NULL AND Status <> N'';
GO

PRINT N'ستون‌هایِ گمشدهٔ Pur.PurchaseOrders رفع شد.';
GO
