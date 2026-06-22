-- =============================================================================
--  CON-C2-4 — دادهٔ دموِ پیمانکاری (یک پیمان + پیش‌پرداخت + ضمانت‌نامه).
--  محتوای قابلِ‌نمایش؛ صورت‌وضعیت/سند از طریقِ کامند ثبت می‌شود (توازنِ سند).
--  idempotent (IF NOT EXISTS)؛ بدونِ USE.
-- =============================================================================
GO
DECLARE @Cid INT = (SELECT TOP 1 Id FROM Cfg.Companies ORDER BY Id);
IF @Cid IS NULL OR OBJECT_ID('Con.Projects','U') IS NULL RETURN;

-- کارفرما = یک طرف‌حسابِ موجود (ترجیحاً مشتری Code LIKE 'M%'؛ وگرنه هر طرف‌حساب).
DECLARE @Emp INT = (SELECT TOP 1 Id FROM Crm.Parties WHERE CompanyId=@Cid AND Code LIKE N'M%' ORDER BY Id);
IF @Emp IS NULL SET @Emp = (SELECT TOP 1 Id FROM Crm.Parties WHERE CompanyId=@Cid ORDER BY Id);
IF @Emp IS NULL RETURN;

-- ── پیمان (فهرست‌بها) با درصدهای متعارف ──
IF NOT EXISTS (SELECT 1 FROM Con.Projects WHERE CompanyId=@Cid AND Code=N'PRJ-1001')
    INSERT INTO Con.Projects (CompanyId, Code, Title, EmployerPartyId, ContractType, ContractAmount,
        StartDate, DurationDays, AdvancePercent, RetentionPercent, InsuranceWithholdPercent, TaxWithholdPercent, Status)
    VALUES (@Cid, N'PRJ-1001', N'احداثِ ساختمانِ اداری — فاز ۱', @Emp, 0, 50000000000,
        N'1405/01/15', 365, 25, 10, 5, 3, 0);

DECLARE @Prj INT = (SELECT TOP 1 Id FROM Con.Projects WHERE CompanyId=@Cid AND Code=N'PRJ-1001');

-- ── پیش‌پرداختِ اولیه (۲۵٪ مبلغِ پیمان) ──
IF @Prj IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Con.AdvancePayments WHERE CompanyId=@Cid AND ContractProjectId=@Prj AND Note=N'دموِ اولیه')
    INSERT INTO Con.AdvancePayments (CompanyId, ContractProjectId, Amount, Date, PaymentMethod, Note)
    VALUES (@Cid, @Prj, 12500000000, N'1405/01/20', N'بانک', N'دموِ اولیه');

-- ── ضمانت‌نامهٔ حسنِ‌انجامِ کار ──
IF @Prj IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Con.Guarantees WHERE CompanyId=@Cid AND ContractProjectId=@Prj AND Note=N'دموِ اولیه')
    INSERT INTO Con.Guarantees (CompanyId, ContractProjectId, Type, Bank, Amount, IssueDate, ExpiryDate, Status, Note)
    VALUES (@Cid, @Prj, 0, N'بانکِ ملت', 2500000000, N'1405/01/18', N'1406/01/18', 0, N'دموِ اولیه');
GO
