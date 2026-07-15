-- =============================================================================
-- U-SEC-RECOVERY — کدِ بازیابیِ رمزِ عبور (درخواستِ کاربر @2026-07-15)
--   وقتی کاربر در ویزاردِ راه‌اندازیِ اولیه رمزِ ادمین را تعیین می‌کند، یک کدِ بازیابیِ تصادفی هم
--   ساخته و فقط همان یک‌بار نمایش داده می‌شود. اگر بعداً رمز فراموش شود، از صفحهٔ ورود با همین کد
--   می‌شود رمزِ جدید تعیین کرد — بدونِ نیازِ ایمیل/پیامک (این برنامه آفلاین/محلی است).
--   کد هرگز خام ذخیره نمی‌شود، فقط هش (همان PBKDF2ِ رمزِ عبور، جدولِ نمک/هشِ جداگانه).
-- =============================================================================
USE SamaHesab;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Sec.Users') AND name = 'RecoveryCodeHash')
    ALTER TABLE Sec.Users ADD RecoveryCodeHash NVARCHAR(256) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Sec.Users') AND name = 'RecoveryCodeSalt')
    ALTER TABLE Sec.Users ADD RecoveryCodeSalt NVARCHAR(100) NULL;
GO
