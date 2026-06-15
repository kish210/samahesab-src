-- =============================================================================
-- فاز ۱۰ DT-2 — موتورِ قالبِ پویای اسناد: جدولِ قالب‌ها + seedِ ۲ قالبِ پیش‌فرضِ فاکتور فروش.
-- idempotent: امن برای اجرای مکرر (توسطِ migration-runner).
-- =============================================================================
USE SamaHesab;
GO

IF SCHEMA_ID('Cfg') IS NULL EXEC('CREATE SCHEMA Cfg');
GO

IF OBJECT_ID('Cfg.DocumentTemplates','U') IS NULL
BEGIN
    CREATE TABLE Cfg.DocumentTemplates (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId     INT NOT NULL DEFAULT 1,
        DocumentType  NVARCHAR(60)  NOT NULL,
        Name          NVARCHAR(150) NOT NULL,
        PaperSize     NVARCHAR(20)  NOT NULL DEFAULT 'A4P',
        HeaderHtml    NVARCHAR(MAX) NULL,
        BodyHtml      NVARCHAR(MAX) NOT NULL,
        FooterHtml    NVARCHAR(MAX) NULL,
        IsDefault     BIT NOT NULL DEFAULT 0,
        IsActive      BIT NOT NULL DEFAULT 1,
        IsSystem      BIT NOT NULL DEFAULT 0,
        CreatedAt     DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt     DATETIME2 NULL,
        CreatedByUserId INT NULL,
        UpdatedByUserId INT NULL
    );
    CREATE INDEX IX_DocTemplates_Type ON Cfg.DocumentTemplates(CompanyId, DocumentType, IsActive);
END
GO

-- seedِ قالب‌های پیش‌فرضِ فاکتور فروش (فقط اگر هیچ قالبِ SalesInvoice نبود)
IF NOT EXISTS (SELECT 1 FROM Cfg.DocumentTemplates WHERE DocumentType = 'SalesInvoice')
BEGIN
    INSERT INTO Cfg.DocumentTemplates (CompanyId, DocumentType, Name, PaperSize, BodyHtml, IsDefault, IsSystem)
    VALUES
    (1, 'SalesInvoice', 'فاکتور رسمی (A4)', 'A4P',
     N'<div style="font-family:Tahoma;direction:rtl;padding:16px">
       <h2 style="text-align:center">فاکتور فروش</h2>
       <table style="width:100%"><tr><td>شماره: {InvoiceNumber}</td><td>تاریخ: {InvoiceDate}</td></tr>
       <tr><td>مشتری: {CustomerName}</td><td>کد: {CustomerCode}</td></tr></table>
       <table border="1" cellspacing="0" cellpadding="6" style="width:100%;border-collapse:collapse;margin-top:10px">
         <thead><tr><th>#</th><th>کالا</th><th>تعداد</th><th>فی</th><th>مبلغ</th></tr></thead>
         <tbody>[[ROWS]]<tr><td>{#}</td><td>{ProductName}</td><td>{Quantity}</td><td>{UnitPrice}</td><td>{LineTotal}</td></tr>[[/ROWS]]</tbody>
       </table>
       <h3 style="text-align:left">جمع کل: {TotalAmount} ریال — مالیات: {Tax} — تخفیف: {Discount}</h3>
       <p style="text-align:center;color:#666">{BranchName}</p></div>',
     1, 1),
    (1, 'SalesInvoice', 'رسیدِ حرارتی (۸۰م م)', 'Thermal80',
     N'<div style="font-family:Tahoma;direction:rtl;width:280px;font-size:12px">
       <div style="text-align:center;font-weight:bold">{BranchName}</div>
       <div>فاکتور: {InvoiceNumber} — {InvoiceDate}</div>
       <div>مشتری: {CustomerName}</div><hr/>
       [[ROWS]]<div>{ProductName} × {Quantity} = {LineTotal}</div>[[/ROWS]]<hr/>
       <div style="font-weight:bold">جمع: {TotalAmount} ریال</div></div>',
     0, 1);
END
GO

PRINT N'DT-2: Cfg.DocumentTemplates ensured + seed.';
GO
