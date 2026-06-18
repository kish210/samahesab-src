-- =============================================================================
-- 35_PartyUnification.sql — ادغامِ طرف‌حساب (Party) — مرحلهٔ A (افزایشی، امن، idempotent)
-- ساختِ جدولِ یکپارچهٔ Crm.Parties و پرکردنِ آن از Customers + Suppliers.
-- ⚠️ غیرمخرّب: جداولِ Customers/Suppliers و FKها دست‌نخورده می‌مانند (cutover مرحلهٔ بعد).
-- dedup بر اساسِ کد ملی (NationalCode) در یک شرکت: اگر شخصی هم مشتری هم تأمین‌کننده باشد،
-- یک رکوردِ طرف‌حساب با هر دو نقش ساخته می‌شود.
-- =============================================================================

IF OBJECT_ID(N'Crm.Parties') IS NULL
BEGIN
    CREATE TABLE Crm.Parties (
        Id               INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId        INT NOT NULL,
        Code             NVARCHAR(20) NOT NULL,
        PartyType        NVARCHAR(10) NOT NULL DEFAULT N'حقیقی',
        FirstName        NVARCHAR(100),
        LastName         NVARCHAR(100),
        CompanyName      NVARCHAR(200),
        NationalCode     NVARCHAR(11),
        EconomicCode     NVARCHAR(12),
        AccountId        INT,
        Phone            NVARCHAR(20),
        Mobile           NVARCHAR(15),
        Email            NVARCHAR(100),
        Province         NVARCHAR(50),
        City             NVARCHAR(50),
        Address          NVARCHAR(500),
        PostalCode       NVARCHAR(10),
        CreditLimit      DECIMAL(18,2) NOT NULL DEFAULT 0,
        CreditDays       INT NOT NULL DEFAULT 0,
        PriceLevel       NVARCHAR(20) NOT NULL DEFAULT N'خرده',
        Discount         DECIMAL(5,2) NOT NULL DEFAULT 0,
        LoyaltyPoints    INT NOT NULL DEFAULT 0,
        Balance          DECIMAL(18,2) NOT NULL DEFAULT 0,
        IsActive         BIT NOT NULL DEFAULT 1,
        Notes            NVARCHAR(2000),
        ContactPerson    NVARCHAR(100),
        Visitor          NVARCHAR(100),
        IsCustomer       BIT NOT NULL DEFAULT 0,
        IsSupplier       BIT NOT NULL DEFAULT 0,
        LegacyCustomerId INT,
        LegacySupplierId INT,
        CreatedAt        DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt        DATETIME2,
        CreatedByUserId  INT,
        UpdatedByUserId  INT
    );
    CREATE INDEX IX_Parties_Company ON Crm.Parties(CompanyId);
    CREATE INDEX IX_Parties_National ON Crm.Parties(CompanyId, NationalCode);
END
GO

-- ── ۱) مشتری‌ها → طرف‌حساب (هر مشتری که هنوز منتقل نشده) ──
INSERT INTO Crm.Parties
    (CompanyId, Code, PartyType, FirstName, LastName, CompanyName, NationalCode, EconomicCode,
     AccountId, Phone, Mobile, Email, Province, City, Address, PostalCode,
     CreditLimit, CreditDays, PriceLevel, Discount, LoyaltyPoints, Balance, IsActive, Notes,
     ContactPerson, Visitor, IsCustomer, IsSupplier, LegacyCustomerId, CreatedAt)
SELECT c.CompanyId, c.Code, c.CustomerType, c.FirstName, c.LastName, c.CompanyName, c.NationalCode, c.EconomicCode,
       c.AccountId, c.Phone, c.Mobile, c.Email, c.Province, c.City, c.Address, c.PostalCode,
       c.CreditLimit, c.CreditDays, c.PriceLevel, c.Discount, c.LoyaltyPoints, c.Balance, c.IsActive, c.Notes,
       c.ContactPerson, c.Visitor, 1, 0, c.Id, c.CreatedAt
FROM Crm.Customers c
WHERE NOT EXISTS (SELECT 1 FROM Crm.Parties p WHERE p.LegacyCustomerId = c.Id);
GO

-- ── ۲) تأمین‌کننده‌ها: اگر هم‌شخصِ مشتری باشد (کد ملیِ یکسانِ غیرخالی) → فقط نقشِ تأمین‌کننده اضافه شود ──
UPDATE p
SET p.IsSupplier = 1, p.LegacySupplierId = s.Id, p.UpdatedAt = GETDATE()
FROM Crm.Parties p
JOIN Crm.Suppliers s
  ON s.CompanyId = p.CompanyId
 AND s.NationalCode IS NOT NULL AND LTRIM(RTRIM(s.NationalCode)) <> ''
 AND s.NationalCode = p.NationalCode
WHERE p.LegacySupplierId IS NULL AND p.IsSupplier = 0;
GO

-- ── ۳) بقیهٔ تأمین‌کننده‌ها (بدونِ تطبیق) → طرف‌حسابِ جدید با نقشِ تأمین‌کننده ──
INSERT INTO Crm.Parties
    (CompanyId, Code, PartyType, FirstName, LastName, CompanyName, NationalCode, EconomicCode,
     AccountId, Phone, Mobile, Email, Province, City, Address, PostalCode,
     CreditLimit, CreditDays, Balance, IsActive, Notes,
     IsCustomer, IsSupplier, LegacySupplierId, CreatedAt)
SELECT s.CompanyId, s.Code, s.SupplierType, s.FirstName, s.LastName, s.CompanyName, s.NationalCode, s.EconomicCode,
       s.AccountId, s.Phone, s.Mobile, s.Email, s.Province, s.City, s.Address, s.PostalCode,
       s.CreditLimit, s.CreditDays, s.Balance, s.IsActive, s.Notes,
       0, 1, s.Id, s.CreatedAt
FROM Crm.Suppliers s
WHERE NOT EXISTS (SELECT 1 FROM Crm.Parties p WHERE p.LegacySupplierId = s.Id);
GO
