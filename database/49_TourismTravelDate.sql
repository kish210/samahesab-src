-- =============================================================================
-- گردشگری — افزودنِ ستونِ تاریخِ سفر (TravelDate) به خطِ فروشِ گردشگری.
-- شمسی، اختیاری — برای نمایش در ردیف و چاپِ واچر. idempotent؛ بدونِ USE.
-- =============================================================================

IF OBJECT_ID('Tur.SaleLines', 'U') IS NOT NULL
   AND COL_LENGTH('Tur.SaleLines', 'TravelDate') IS NULL
    ALTER TABLE Tur.SaleLines ADD TravelDate nvarchar(10) NULL;
GO
