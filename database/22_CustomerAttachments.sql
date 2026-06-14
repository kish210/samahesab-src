-- =============================================================================
-- 22_CustomerAttachments.sql — کارِ ۹ (پیوستِ مشتری، لِین C2)
-- جدولِ اسنادِ ضمیمهٔ مشتری (idempotent). فایل در سیستم‌فایلِ محلی ذخیره می‌شود.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
               WHERE s.name = N'Crm' AND t.name = N'CustomerAttachments')
BEGIN
    CREATE TABLE Crm.CustomerAttachments (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
        CustomerId      INT NOT NULL REFERENCES Crm.Customers(Id) ON DELETE CASCADE,
        FileName        NVARCHAR(260) NOT NULL,
        StoredPath      NVARCHAR(500) NOT NULL,
        ContentType     NVARCHAR(100),
        FileSize        BIGINT NOT NULL DEFAULT 0,
        UploadedAt      NVARCHAR(10) NOT NULL,
        Description     NVARCHAR(500),
        CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt       DATETIME2
    );
END
GO
