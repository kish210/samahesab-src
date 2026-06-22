-- =============================================================================
--  TUR-C2-5 — دادهٔ دموِ گردشگری (کاتالوگ + ودیعهٔ تأمین‌کننده).
--  فقط محتوای قابلِ‌نمایش (گروه/محصول/ودیعه) را می‌سازد؛ فروش/سند از طریقِ کامند ثبت می‌شود.
--  idempotent (با IF NOT EXISTS)؛ بدونِ USE تا روی هر DBِ پیکربندی‌شده اجرا شود.
-- =============================================================================
GO
DECLARE @Cid INT = (SELECT TOP 1 Id FROM Cfg.Companies ORDER BY Id);

-- جداولِ Tur باید موجود باشند (مهاجرتِ 42 اعمال شده).
IF @Cid IS NULL OR OBJECT_ID('Tur.Products','U') IS NULL RETURN;

-- تأمین‌کننده‌ها از دموِ پایه (Code LIKE 'T%') — اگر نبود، هر تأمین‌کننده‌ای.
DECLARE @S1 INT = (SELECT TOP 1 Id FROM Crm.Parties WHERE CompanyId=@Cid AND IsSupplier=1 ORDER BY Id);
DECLARE @S2 INT = (SELECT TOP 1 Id FROM Crm.Parties WHERE CompanyId=@Cid AND IsSupplier=1 AND Id<>@S1 ORDER BY Id);
IF @S1 IS NULL RETURN;             -- بدونِ تأمین‌کننده، دموِ گردشگری معنا ندارد.
IF @S2 IS NULL SET @S2 = @S1;

-- ── گروه‌های محصول ──
IF NOT EXISTS (SELECT 1 FROM Tur.ProductGroups WHERE CompanyId=@Cid AND Name=N'تور')
    INSERT INTO Tur.ProductGroups (CompanyId, Name) VALUES (@Cid, N'تور');
IF NOT EXISTS (SELECT 1 FROM Tur.ProductGroups WHERE CompanyId=@Cid AND Name=N'بلیط')
    INSERT INTO Tur.ProductGroups (CompanyId, Name) VALUES (@Cid, N'بلیط');

DECLARE @GTour INT = (SELECT TOP 1 Id FROM Tur.ProductGroups WHERE CompanyId=@Cid AND Name=N'تور');
DECLARE @GTkt  INT = (SELECT TOP 1 Id FROM Tur.ProductGroups WHERE CompanyId=@Cid AND Name=N'بلیط');

-- ── محصولات/خدمات ──
IF NOT EXISTS (SELECT 1 FROM Tur.Products WHERE CompanyId=@Cid AND Name=N'گشتِ دورِ جزیره')
    INSERT INTO Tur.Products (CompanyId, Name, SupplierPartyId, PurchasePrice, DefaultSalePrice, ProductGroupId, RequiresPassengerList)
    VALUES (@Cid, N'گشتِ دورِ جزیره', @S1, 7000000, 10000000, @GTour, 1);
IF NOT EXISTS (SELECT 1 FROM Tur.Products WHERE CompanyId=@Cid AND Name=N'بلیطِ پروازِ داخلی')
    INSERT INTO Tur.Products (CompanyId, Name, SupplierPartyId, PurchasePrice, DefaultSalePrice, ProductGroupId, RequiresPassengerList)
    VALUES (@Cid, N'بلیطِ پروازِ داخلی', @S2, 4200000, 4800000, @GTkt, 0);
IF NOT EXISTS (SELECT 1 FROM Tur.Products WHERE CompanyId=@Cid AND Name=N'ترانسفرِ فرودگاه')
    INSERT INTO Tur.Products (CompanyId, Name, SupplierPartyId, PurchasePrice, DefaultSalePrice, ProductGroupId, RequiresPassengerList)
    VALUES (@Cid, N'ترانسفرِ فرودگاه', @S1, 800000, 1200000, NULL, 0);

-- ── شارژِ اولیهٔ ودیعهٔ تأمین‌کننده‌ها (Note='دموِ اولیه' به‌عنوانِ کلیدِ idempotency) ──
IF NOT EXISTS (SELECT 1 FROM Tur.SupplierDeposits WHERE CompanyId=@Cid AND SupplierPartyId=@S1 AND Note=N'دموِ اولیه')
    INSERT INTO Tur.SupplierDeposits (CompanyId, SupplierPartyId, Amount, Date, PaymentMethod, Note)
    VALUES (@Cid, @S1, 200000000, N'1405/03/01', N'بانک', N'دموِ اولیه');
IF @S2 <> @S1 AND NOT EXISTS (SELECT 1 FROM Tur.SupplierDeposits WHERE CompanyId=@Cid AND SupplierPartyId=@S2 AND Note=N'دموِ اولیه')
    INSERT INTO Tur.SupplierDeposits (CompanyId, SupplierPartyId, Amount, Date, PaymentMethod, Note)
    VALUES (@Cid, @S2, 120000000, N'1405/03/05', N'بانک', N'دموِ اولیه');
GO
