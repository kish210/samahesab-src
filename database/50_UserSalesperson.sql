-- =============================================================================
-- SP-1 — نگاشتِ کاربر به «فروشنده» (Crm.Parties) برای پنلِ فروشِ گردشگریِ فروشنده‌محور.
-- اگر کاربر SalespersonPartyId داشته باشد، فروشِ گردشگری فروشنده را خودکار از او می‌گیرد.
-- اختیاری، بدونِ FK (جداسازیِ امنیت از CRM). idempotent؛ بدونِ USE.
-- =============================================================================

IF OBJECT_ID('Sec.Users', 'U') IS NOT NULL
   AND COL_LENGTH('Sec.Users', 'SalespersonPartyId') IS NULL
    ALTER TABLE Sec.Users ADD SalespersonPartyId INT NULL;
GO
