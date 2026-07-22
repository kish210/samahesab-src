-- =============================================================================
-- U-CON-DROPSVC — حذفِ فیچرِ «قراردادِ خدماتیِ تکرارشونده» به‌درخواستِ کاربر (@2026-07-22).
-- هم‌الگو با database/58_DropRecurringInvoices.sql. فرزند قبل از والد؛ idempotent؛ بدونِ USE.
-- =============================================================================
USE SamaHesab;
GO

IF OBJECT_ID('Con.ServiceContractExtraItems', 'U') IS NOT NULL
    DROP TABLE Con.ServiceContractExtraItems;
GO
IF OBJECT_ID('Con.ServiceContracts', 'U') IS NOT NULL
    DROP TABLE Con.ServiceContracts;
GO
