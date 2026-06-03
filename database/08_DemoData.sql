-- =============================================================================
--  Sama Hesab - Demo data (products, customers, suppliers, warehouses)
--  Run with:  sqlcmd -S .\SQLEXPRESS -E -d SamaHesab -f 65001 -i 08_DemoData.sql
--  ProductType / ValuationMethod use the EF enum NAMES on purpose.
-- =============================================================================
USE SamaHesab;
GO
DECLARE @Cid INT = (SELECT TOP 1 Id FROM Cfg.Companies ORDER BY Id);
DECLARE @Unit INT = (SELECT TOP 1 Id FROM Cfg.Units ORDER BY Id);

-- ── Warehouses ──────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Inv.Warehouses WHERE CompanyId=@Cid AND Code=N'WH1')
    INSERT INTO Inv.Warehouses(CompanyId, Code, Name, IsDefault, IsActive)
    VALUES (@Cid, N'WH1', N'انبار مرکزی', 1, 1), (@Cid, N'WH2', N'انبار شعبه ۱', 0, 1);

-- ── Products (ProductType=Product, ValuationMethod=WeightedAverage) ──────────
IF NOT EXISTS (SELECT 1 FROM Inv.Products WHERE CompanyId=@Cid AND Code=N'K1001')
INSERT INTO Inv.Products
    (CompanyId, Code, Barcode, Name, UnitId, ProductType, ValuationMethod,
     PurchasePrice, SalePrice, WholesalePrice, ConsumerPrice, MinStock, TaxRate, IsActive)
VALUES
 (@Cid, N'K1001', N'6260100100013', N'روغن موتور ۵ لیتری بهران',      @Unit, N'Product', N'WeightedAverage',  850000, 1050000,  980000, 1100000, 5, 9, 1),
 (@Cid, N'K1002', N'6260100100020', N'فیلتر روغن پراید',              @Unit, N'Product', N'WeightedAverage',   45000,   68000,   60000,   72000, 10, 9, 1),
 (@Cid, N'K1003', N'6260100100037', N'لاستیک ۱۷۵/۷۰R13 بارز',        @Unit, N'Product', N'WeightedAverage', 1800000, 2250000, 2100000, 2350000, 4, 9, 1),
 (@Cid, N'K1004', N'6260100100044', N'باتری ۶۰ آمپر سپاهان',          @Unit, N'Product', N'WeightedAverage', 2200000, 2750000, 2600000, 2850000, 3, 9, 1),
 (@Cid, N'K1005', N'6260100100051', N'شمع موتور NGK',                 @Unit, N'Product', N'WeightedAverage',   38000,   55000,   49000,   58000, 20, 9, 1),
 (@Cid, N'K1006', N'6260100100068', N'تسمه تایم تیبا',                @Unit, N'Product', N'WeightedAverage',  120000,  175000,  160000,  185000, 8, 9, 1),
 (@Cid, N'K1007', N'6260100100075', N'لنت ترمز جلو پژو ۲۰۶',          @Unit, N'Product', N'WeightedAverage',  210000,  295000,  270000,  310000, 6, 9, 1),
 (@Cid, N'K1008', N'6260100100082', N'مایع خنک‌کننده ضدیخ ۴ لیتری',   @Unit, N'Product', N'WeightedAverage',   95000,  140000,  125000,  150000, 12, 9, 1),
 (@Cid, N'K1009', N'6260100100099', N'برف‌پاک‌کن ۲۴ اینچ',            @Unit, N'Product', N'WeightedAverage',   85000,  130000,  115000,  140000, 15, 9, 1),
 (@Cid, N'K1010', N'6260100100105', N'فیلتر هوای موتور',              @Unit, N'Product', N'WeightedAverage',   65000,   98000,   88000,  105000, 18, 9, 1),
 (@Cid, N'K1011', N'6260100100112', N'روغن ترمز DOT4',                @Unit, N'Product', N'WeightedAverage',   55000,   82000,   74000,   88000, 10, 9, 1),
 (@Cid, N'K1012', N'6260100100129', N'چراغ جلو پراید',                @Unit, N'Product', N'WeightedAverage',  320000,  450000,  410000,  480000, 5, 9, 1);

-- ── Customers ───────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Crm.Customers WHERE CompanyId=@Cid AND Code=N'M1001')
INSERT INTO Crm.Customers
    (CompanyId, Code, CustomerType, FirstName, LastName, CompanyName, Mobile, City, PriceLevel, CreditLimit, Balance, IsActive)
VALUES
 (@Cid, N'M1001', N'حقیقی', N'علی',   N'احمدی',   NULL,                 N'09121110001', N'تهران',  N'خرده', 50000000,  12500000, 1),
 (@Cid, N'M1002', N'حقوقی', NULL,     NULL,       N'شرکت آلفا تجارت',   N'02144550010', N'تهران',  N'عمده', 500000000, 45200000, 1),
 (@Cid, N'M1003', N'حقیقی', N'محمد',  N'رضایی',   NULL,                 N'09351110003', N'کرج',    N'خرده', 30000000,  -2100000, 1),
 (@Cid, N'M1004', N'حقوقی', NULL,     NULL,       N'بازرگانی پارس خودرو',N'03132220040', N'اصفهان', N'عمده', 800000000, 78000000, 1),
 (@Cid, N'M1005', N'حقیقی', N'زهرا',  N'کریمی',   NULL,                 N'09171110005', N'شیراز',  N'خرده', 20000000,   3400000, 1),
 (@Cid, N'M1006', N'حقیقی', N'حسین',  N'موسوی',   NULL,                 N'09131110006', N'یزد',    N'ویژه', 40000000,         0, 1);

-- ── Suppliers ───────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Crm.Suppliers WHERE CompanyId=@Cid AND Code=N'T1001')
INSERT INTO Crm.Suppliers
    (CompanyId, Code, SupplierType, FirstName, LastName, CompanyName, Mobile, City, Balance, IsActive)
VALUES
 (@Cid, N'T1001', N'حقوقی', NULL, NULL, N'پخش قطعات بهران',     N'02155440010', N'تهران',  -32000000, 1),
 (@Cid, N'T1002', N'حقوقی', NULL, NULL, N'لاستیک بارز نمایندگی',N'03433220020', N'کرمان',  -15000000, 1),
 (@Cid, N'T1003', N'حقیقی', N'رضا', N'نوری', NULL,              N'09124440030', N'تهران',   -5000000, 1),
 (@Cid, N'T1004', N'حقوقی', NULL, NULL, N'باتری سپاهان پخش',    N'03134440040', N'اصفهان', -28000000, 1);
-- ── Bank accounts (AccountId references any leaf account) ───────────────────
DECLARE @Acc INT = (SELECT TOP 1 Id FROM Acc.Accounts WHERE CompanyId=@Cid AND IsLeaf=1 ORDER BY Id);
IF @Acc IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Acc.BankAccounts WHERE CompanyId=@Cid)
INSERT INTO Acc.BankAccounts
    (CompanyId, AccountId, BankName, AccountNumber, ShebaNumber, CardNumber, BranchName, OpeningBalance, IsActive)
VALUES
 (@Cid, @Acc, N'بانک ملت',    N'1234567890',  N'IR120120000000001234567890', N'6104-3370-0000-0001', N'شعبه مرکزی', 250000000, 1),
 (@Cid, @Acc, N'بانک ملی',    N'9876543210',  N'IR550170000000009876543210', N'6037-9900-0000-0002', N'شعبه ولیعصر', 180000000, 1),
 (@Cid, @Acc, N'بانک صادرات', N'5555444433',  N'IR330190000000005555444433', N'6037-6900-0000-0003', N'شعبه آزادی', 95000000, 1);
-- ── Employees ───────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Hrm.Employees WHERE CompanyId=@Cid AND Code=N'E1001')
INSERT INTO Hrm.Employees
    (CompanyId, Code, NationalCode, FirstName, LastName, Gender, MaritalStatus, Education,
     Mobile, HireDate, ContractType, BaseSalary, IsActive)
VALUES
 (@Cid, N'E1001', N'0012345678', N'علی',   N'احمدی',  N'مرد', N'متاهل', N'کارشناسی', N'09121110001', N'1402/01/15', N'دائم',     120000000, 1),
 (@Cid, N'E1002', N'0023456789', N'مریم',  N'رضایی',  N'زن',  N'مجرد',  N'کارشناسی ارشد', N'09122220002', N'1402/03/01', N'دائم', 150000000, 1),
 (@Cid, N'E1003', N'0034567890', N'رضا',   N'محمدی',  N'مرد', N'متاهل', N'دیپلم',    N'09123330003', N'1401/07/10', N'پیمانی',  95000000, 1),
 (@Cid, N'E1004', N'0045678901', N'زهرا',  N'کریمی',  N'زن',  N'متاهل', N'کارشناسی', N'09124440004', N'1403/02/20', N'موقت',    85000000, 1);
-- ── Initial stock for every product in the main warehouse ──────────────────
DECLARE @WH INT = (SELECT TOP 1 Id FROM Inv.Warehouses WHERE CompanyId=@Cid AND Code=N'WH1');
IF @WH IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Inv.StockItems WHERE WarehouseId=@WH)
INSERT INTO Inv.StockItems (ProductId, WarehouseId, Quantity, AverageCost, LastCost, LastUpdated)
SELECT p.Id, @WH,
       ABS(CHECKSUM(NEWID())) % 50 + 10,   -- random qty 10..59
       p.PurchasePrice, p.PurchasePrice, GETDATE()
FROM Inv.Products p WHERE p.CompanyId=@Cid;
GO
PRINT 'Demo data inserted.';
GO
