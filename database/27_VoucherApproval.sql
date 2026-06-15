-- =============================================================================
-- T22: گردش‌کارِ تأییدِ سند — ستونِ ApprovalStatus روی Acc.Vouchers.
-- null = خارج از گردش‌کار (قطعیِ مستقیم، سازگارِ عقب‌رو) · 1=در انتظار · 2=تأییدشده · 3=ردشده.
-- idempotent (مهاجرتِ افزایشی؛ توسطِ DatabaseMigratorِ استارت‌آپ هم خودکار اعمال می‌شود).
-- =============================================================================
USE SamaHesab;
GO

IF OBJECT_ID('Acc.Vouchers','U') IS NOT NULL
   AND COL_LENGTH('Acc.Vouchers','ApprovalStatus') IS NULL
    ALTER TABLE Acc.Vouchers ADD ApprovalStatus INT NULL;
GO

PRINT N'T22: ستونِ Acc.Vouchers.ApprovalStatus ensured.';
GO
