-- 80_BankReconciliation.sql — U-BANK-RECON-WEB: ماندگاریِ دیتابیسیِ مغایرت‌گیری بانکی.
-- جایگزینِ فایلِ محلیِ دسکتاپ (bank-recon.json) تا روی هر دو ماشین یکسان و هم‌راستا با
-- قاعدهٔ «تنها کانالِ مشترک، DB محلی + git است». idempotent.
IF SCHEMA_ID('Acc') IS NULL EXEC('CREATE SCHEMA Acc');
GO

IF OBJECT_ID('Acc.BankReconciledItems', 'U') IS NULL
BEGIN
    CREATE TABLE Acc.BankReconciledItems (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId       INT           NOT NULL,
        BankAccountId   INT           NOT NULL,
        VoucherItemId   INT           NOT NULL,
        ReconciledDate  NVARCHAR(20)  NOT NULL,               -- شمسی «yyyy/MM/dd»
        CreatedAt       DATETIME2     NOT NULL CONSTRAINT DF_BankRecon_Items_Created DEFAULT(SYSDATETIME()),
        UpdatedAt       DATETIME2     NULL,
        CreatedByUserId INT           NULL,
        UpdatedByUserId INT           NULL,
        CONSTRAINT UQ_BankRecon_Items UNIQUE (BankAccountId, VoucherItemId)
    );
    CREATE INDEX IX_BankRecon_Items_Bank ON Acc.BankReconciledItems(BankAccountId);
END
GO
