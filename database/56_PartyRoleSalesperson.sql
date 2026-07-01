-- 56_PartyRoleSalesperson.sql — چندوجهی‌کردنِ نقشِ اشخاص: افزودنِ نقشِ «فروشنده» به Crm.Parties
-- (تکمیلِ IsCustomer/IsSupplier/IsEmployee → یک شخص می‌تواند هم‌زمان تأمین‌کننده/خریدار/کارمند/فروشنده باشد).
-- idempotent — توسطِ DatabaseMigrator در استارت‌آپ اجرا می‌شود.
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Crm')
   AND EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
               WHERE s.name = 'Crm' AND t.name = 'Parties')
   AND NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID('Crm.Parties') AND name = 'IsSalesperson')
BEGIN
    ALTER TABLE Crm.Parties ADD IsSalesperson BIT NOT NULL DEFAULT 0;
END
GO
