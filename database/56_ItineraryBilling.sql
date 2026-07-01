-- 56_ItineraryBilling.sql — MOD-TIT-BILL: صدورِ سند هنگامِ تأییدِ مهمانِ برنامهٔ اقامتی.
-- زمینهٔ لازم برای ساختِ سند/دریافتنی روی سرسندِ GuestItineraries (شعبه/فروشنده/لینکِ سند).
-- idempotent — DatabaseMigrator در استارت‌آپ اجرا می‌کند.
IF OBJECT_ID('Tur.GuestItineraries', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('Tur.GuestItineraries', 'BranchId') IS NULL
        ALTER TABLE Tur.GuestItineraries ADD BranchId INT NOT NULL CONSTRAINT DF_TurGuestIt_Branch DEFAULT 1;

    IF COL_LENGTH('Tur.GuestItineraries', 'SalespersonPartyId') IS NULL
        ALTER TABLE Tur.GuestItineraries ADD SalespersonPartyId INT NULL;

    IF COL_LENGTH('Tur.GuestItineraries', 'SaleId') IS NULL
        ALTER TABLE Tur.GuestItineraries ADD SaleId INT NULL;
END
GO
