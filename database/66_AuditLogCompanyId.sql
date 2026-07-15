-- =============================================================================
-- U-SEC-AUDIT-COMPANY — افزودنِ CompanyId به Sec.AuditLogs
--   پیش‌تر این جدول اصلاً ستونِ CompanyId نداشت. در تک‌شرکتیِ استانداردِ این برنامه (هر نصب = یک
--   DBِ محلیِ یک‌شرکته) بی‌ضرر بود؛ ولی اگر روزی چند شرکت از یک DBِ مشترک استفاده کنند، لاگِ
--   حسابرسیِ همهٔ شرکت‌ها قاطی و برایِ هر کاربرِ دارایِ Security.Manage قابلِ‌دیدن می‌شد.
--   ردیف‌هایِ قدیمی (پیش از این مهاجرت) به تنها شرکتِ موجود در همان DB نسبت داده می‌شوند —
--   درست برایِ مدلِ استانداردِ «هر DB = یک شرکت»ِ این برنامه.
-- =============================================================================
USE SamaHesab;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Sec.AuditLogs') AND name = 'CompanyId')
    ALTER TABLE Sec.AuditLogs ADD CompanyId INT NULL;
GO

-- بک‌فیلِ ردیف‌هایِ قدیمی — فقط اگر دقیقاً یک شرکت در این DB وجود دارد (حالتِ استانداردِ این برنامه).
UPDATE Sec.AuditLogs
SET CompanyId = (SELECT TOP 1 Id FROM Cfg.Companies)
WHERE CompanyId IS NULL
  AND (SELECT COUNT(*) FROM Cfg.Companies) = 1;
GO
