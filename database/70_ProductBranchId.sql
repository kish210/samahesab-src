-- 70_ProductBranchId.sql — U-BRANCH-BASEDATA: شعبهٔ اختصاصیِ کالا روی دادهٔ پایه (Inv.Products.BranchId)
-- null = مشترکِ همهٔ شعب (سازگارِ عقب‌رو: ردیف‌های موجود null می‌مانند و برای همه دیدنی‌اند).
-- هم‌الگو با 55_PartyBranchId.sql — Party/Warehouse/Employee این ستون را از قبل داشتند، فقط
-- Product کم داشت (BranchId رویِ داده‌هایِ تراکنشی از قبل بود، رویِ همهٔ دادهٔ پایه نه).
-- idempotent — توسطِ DatabaseMigrator در استارت‌آپ اجرا می‌شود.
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Inv')
   AND EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
               WHERE s.name = 'Inv' AND t.name = 'Products')
   AND NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID('Inv.Products') AND name = 'BranchId')
BEGIN
    ALTER TABLE Inv.Products ADD BranchId INT NULL;
END
GO
