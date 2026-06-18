-- =============================================================================
-- 36_PartyMergeByCode.sql — ادغامِ طرف‌حساب‌های هم‌کد (کلیدِ دومِ تطبیق)
-- اگر مشتری/تأمین‌کننده/کارمند کد ملی نداشتند ولی «کد»ِ یکسان داشتند (همان شخص)،
-- در ۳۵ به‌اشتباه دو رکورد شدند؛ این اسکریپت آن‌ها را در یک طرف‌حساب با ترکیبِ نقش‌ها ادغام می‌کند.
-- idempotent: پس از ادغام، گروهِ هم‌کد دیگر >۱ رکورد ندارد.
-- =============================================================================

SET NOCOUNT ON;

-- گروه‌های هم‌کدِ تکراری (نگه‌داشتنِ کوچک‌ترین Id به‌عنوان رکوردِ اصلی)
;WITH grp AS (
    SELECT CompanyId, Code, MIN(Id) AS KeepId, COUNT(*) AS Cnt
    FROM Crm.Parties GROUP BY CompanyId, Code HAVING COUNT(*) > 1
)
-- تجمیعِ نقش‌ها و Legacyها روی رکوردِ اصلی
UPDATE p SET
    p.IsCustomer = CASE WHEN agg.AnyCustomer = 1 THEN 1 ELSE p.IsCustomer END,
    p.IsSupplier = CASE WHEN agg.AnySupplier = 1 THEN 1 ELSE p.IsSupplier END,
    p.IsEmployee = CASE WHEN agg.AnyEmployee = 1 THEN 1 ELSE p.IsEmployee END,
    p.LegacyCustomerId = COALESCE(p.LegacyCustomerId, agg.AnyCustomerId),
    p.LegacySupplierId = COALESCE(p.LegacySupplierId, agg.AnySupplierId),
    p.LegacyEmployeeId = COALESCE(p.LegacyEmployeeId, agg.AnyEmployeeId),
    p.NationalCode     = COALESCE(p.NationalCode, agg.AnyNationalCode),
    p.UpdatedAt = GETDATE()
FROM Crm.Parties p
JOIN grp ON grp.KeepId = p.Id
CROSS APPLY (
    SELECT
        MAX(CAST(x.IsCustomer AS INT)) AS AnyCustomer,
        MAX(CAST(x.IsSupplier AS INT)) AS AnySupplier,
        MAX(CAST(x.IsEmployee AS INT)) AS AnyEmployee,
        MAX(x.LegacyCustomerId) AS AnyCustomerId,
        MAX(x.LegacySupplierId) AS AnySupplierId,
        MAX(x.LegacyEmployeeId) AS AnyEmployeeId,
        MAX(x.NationalCode)     AS AnyNationalCode
    FROM Crm.Parties x
    WHERE x.CompanyId = grp.CompanyId AND x.Code = grp.Code
) agg;

-- حذفِ رکوردهای تکراری (غیر از رکوردِ اصلی)
;WITH grp AS (
    SELECT CompanyId, Code, MIN(Id) AS KeepId
    FROM Crm.Parties GROUP BY CompanyId, Code HAVING COUNT(*) > 1
)
DELETE p FROM Crm.Parties p
JOIN grp ON grp.CompanyId = p.CompanyId AND grp.Code = p.Code AND p.Id <> grp.KeepId;
GO
