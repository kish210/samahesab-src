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

-- باگِ دومِ کشف‌شده با تستِ زندهٔ نوشتن («ساختِ سفارش از نقطهٔ سفارش»): FiscalYearId/SupplierIدِ
-- اسکیمایِ قدیمی NOT NULL و بدونِ DEFAULT ماندند (چون همین‌طور در 15_PurchaseOrders.sql
-- برنامه‌ریزی شده بود ولی به همان دلیلِ ردیابیِ نامی هرگز اجرا نشد) — درجِ سفارشِ نو با
-- «An error occurred while saving the entity changes» شکست می‌خورد چون Command این دو
-- ستونِ قدیمی را نمی‌فرستد. nullable کردنشان بی‌خطر است (فقط اجازهٔ NULL، بدونِ تغییرِ داده).
IF COL_LENGTH('Pur.PurchaseOrders', 'FiscalYearId') IS NOT NULL
   AND COLUMNPROPERTY(OBJECT_ID('Pur.PurchaseOrders'), 'FiscalYearId', 'AllowsNull') = 0
    ALTER TABLE Pur.PurchaseOrders ALTER COLUMN FiscalYearId INT NULL;
IF COL_LENGTH('Pur.PurchaseOrders', 'SupplierId') IS NOT NULL
   AND COLUMNPROPERTY(OBJECT_ID('Pur.PurchaseOrders'), 'SupplierId', 'AllowsNull') = 0
    ALTER TABLE Pur.PurchaseOrders ALTER COLUMN SupplierId INT NULL;
GO

-- باگِ سومِ کشف‌شده (همان تستِ زندهٔ نوشتن، بعدِ رفعِ دومی): جدولِ فرزندِ
-- Pur.PurchaseOrderItems هم روی اسکیمایِ قدیمی مانده — بدونِ RowNumber/LineTotal
-- (که Entity/EF فعلی نیاز دارد) و با NetAmount قدیمیِ NOT NULL بدونِ DEFAULT.
IF COL_LENGTH('Pur.PurchaseOrderItems', 'RowNumber') IS NULL
    ALTER TABLE Pur.PurchaseOrderItems ADD RowNumber INT NOT NULL CONSTRAINT DF_POI_RowNumber DEFAULT 0;
IF COL_LENGTH('Pur.PurchaseOrderItems', 'LineTotal') IS NULL
    ALTER TABLE Pur.PurchaseOrderItems ADD LineTotal DECIMAL(18,2) NOT NULL CONSTRAINT DF_POI_LineTotal DEFAULT 0;
IF COL_LENGTH('Pur.PurchaseOrderItems', 'NetAmount') IS NOT NULL
   AND COLUMNPROPERTY(OBJECT_ID('Pur.PurchaseOrderItems'), 'NetAmount', 'AllowsNull') = 0
    ALTER TABLE Pur.PurchaseOrderItems ALTER COLUMN NetAmount DECIMAL(18,2) NULL;
GO

-- دادهٔ قدیمیِ NetAmount را به LineTotalِ نو کپی کن تا سطرهایِ موجود صفر نمانند.
-- (batchِ جدا لازم است — ارجاع به ستونِ تازه‌اضافه‌شده در همان batchِ ALTER TABLE ADD
-- با خطایِ «Invalid column name» شکست می‌خورد چون بایندینگِ نام‌ها پیش از اجرا انجام می‌شود.)
IF COL_LENGTH('Pur.PurchaseOrderItems', 'NetAmount') IS NOT NULL
    UPDATE Pur.PurchaseOrderItems SET LineTotal = NetAmount WHERE LineTotal = 0 AND NetAmount <> 0;
GO

PRINT N'ستون‌هایِ گمشدهٔ Pur.PurchaseOrders/PurchaseOrderItems رفع شد.';
GO
