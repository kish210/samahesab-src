-- 55_PartyBranchId.sql — P3: شعبهٔ مالکِ طرف‌حساب روی دادهٔ پایه (Crm.Parties.BranchId)
-- null = مشترکِ همهٔ شعب (سازگارِ عقب‌رو: ردیف‌های موجود null می‌مانند و برای همه دیدنی‌اند).
-- idempotent — توسطِ DatabaseMigrator در استارت‌آپ اجرا می‌شود.
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Crm')
   AND EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
               WHERE s.name = 'Crm' AND t.name = 'Parties')
   AND NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID('Crm.Parties') AND name = 'BranchId')
BEGIN
    ALTER TABLE Crm.Parties ADD BranchId INT NULL;
END
GO
