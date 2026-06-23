-- =============================================================================
-- گردشگری — افزودنِ ستونِ ظرفیت (Capacity) به محصولِ گردشگری.
-- null = ظرفیتِ نامحدود. فروشنده ماندهٔ ظرفیت را از آن می‌بیند. idempotent؛ بدونِ USE.
-- =============================================================================

IF OBJECT_ID('Tur.Products', 'U') IS NOT NULL
   AND COL_LENGTH('Tur.Products', 'Capacity') IS NULL
    ALTER TABLE Tur.Products ADD Capacity int NULL;
GO
