-- =============================================================================
-- 23_ProductDiscountTiers.sql — کارِ ۷/U6 (تخفیفِ پلکانیِ مقداری، لِین C2)
-- پله‌های تخفیفِ مقداریِ هر کالا (idempotent). «مقدار ≥ MinQty → DiscountPercent٪».
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
               WHERE s.name = N'Inv' AND t.name = N'ProductDiscountTiers')
BEGIN
    CREATE TABLE Inv.ProductDiscountTiers (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId       INT NOT NULL REFERENCES Cfg.Companies(Id),
        ProductId       INT NOT NULL REFERENCES Inv.Products(Id) ON DELETE CASCADE,
        MinQty          DECIMAL(18,3) NOT NULL,
        DiscountPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
        CreatedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt       DATETIME2
    );
    CREATE INDEX IX_ProductDiscountTiers_Product ON Inv.ProductDiscountTiers(CompanyId, ProductId, MinQty);
END
GO
