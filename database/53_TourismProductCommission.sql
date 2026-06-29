-- =============================================================================
-- یکپارچه‌سازیِ محصولاتِ گردشگری/اقامتی (TUR-UNIFY): افزودنِ پورسانتِ بازاریاب به محصولِ گردشگری.
-- ستون‌ها به Tur.Products (همان محصولِ کانونی) اضافه می‌شوند. idempotent (COL_LENGTH).
-- =============================================================================

IF COL_LENGTH('Tur.Products', 'MarketerCommissionBasis') IS NULL
    ALTER TABLE Tur.Products ADD MarketerCommissionBasis int NOT NULL CONSTRAINT DF_TurPrd_ComBasis DEFAULT 2;   -- 0=مبلغ 1=٪فروش 2=٪سود
GO
IF COL_LENGTH('Tur.Products', 'MarketerCommissionValue') IS NULL
    ALTER TABLE Tur.Products ADD MarketerCommissionValue decimal(18,2) NOT NULL CONSTRAINT DF_TurPrd_ComVal DEFAULT 0;
GO
