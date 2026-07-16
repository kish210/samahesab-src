-- 72_PartyLedgerEntries.sql — U-PARTY-LEDGER (backlog #9): دفترِ معینِ طرف‌حساب.
-- هر رویدادِ اثرگذار بر مانده (فاکتور/برگشت/دریافت/پرداخت/تسویهٔ کنسینمنت/فروشِ گردشگری) از این
-- پس یک ردیفِ امضادار اینجا ثبت می‌کند؛ Party.Balance کشِ سریع‌خوانی می‌ماند ولی از نظرِ محاسبه‌ای
-- برابرِ Σ(Amount) همین جدول است. idempotent — توسطِ DatabaseMigrator در استارت‌آپ اجرا می‌شود؛
-- backfillِ ماندهٔ ابتدا فقط یک‌بار (همراهِ ساختِ جدول) انجام می‌شود، نه در اجراهایِ بعدی.
IF SCHEMA_ID('Crm') IS NULL EXEC('CREATE SCHEMA Crm');
GO

IF OBJECT_ID('Crm.PartyLedgerEntries', 'U') IS NULL
BEGIN
    CREATE TABLE Crm.PartyLedgerEntries (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId     INT           NOT NULL,
        PartyId       INT           NOT NULL,
        [Date]        NVARCHAR(20)  NOT NULL,
        DocType       NVARCHAR(50)  NOT NULL,
        DocNumber     NVARCHAR(50)  NULL,
        Description   NVARCHAR(500) NULL,
        Amount        DECIMAL(18,2) NOT NULL,
        CreatedAt     DATETIME2     NOT NULL CONSTRAINT DF_PartyLedgerEntries_Created DEFAULT(SYSDATETIME()),
        UpdatedAt     DATETIME2     NULL,
        CreatedByUserId INT NULL,
        UpdatedByUserId INT NULL
    );
    CREATE INDEX IX_PartyLedgerEntries_Company_Party ON Crm.PartyLedgerEntries(CompanyId, PartyId);

    -- backfill یک‌باره: ماندهٔ فعلیِ هر شخص (تاریخچهٔ پیش از این مهاجرت) به‌عنوانِ یک ردیفِ
    -- «ماندهٔ ابتدا» ثبت می‌شود تا از همین لحظه Σ(ledger) = Party.Balance برقرار باشد؛
    -- تلاشی برایِ بازسازیِ ریزِ تاریخچهٔ قدیمی نمی‌شود (غیرِقابلِ‌اعتماد از رویِ دادهٔ فعلی).
    IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
               WHERE s.name = 'Crm' AND t.name = 'Parties')
    BEGIN
        INSERT INTO Crm.PartyLedgerEntries (CompanyId, PartyId, [Date], DocType, DocNumber, Description, Amount)
        SELECT CompanyId, Id, CONVERT(NVARCHAR(20), SYSDATETIME(), 111), N'مانده اولیه', NULL,
               N'ماندهٔ ابتدا (پیاده‌سازیِ دفترِ معین، پیش از این تاریخچهٔ ریز ثبت نشده)', Balance
        FROM Crm.Parties
        WHERE Balance <> 0;
    END
END
GO
