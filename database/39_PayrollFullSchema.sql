-- =============================================================================
-- PAY-C1-1 + PAY-C1-2 — اسکیمـای حقوقِ کاملِ ایرانی (C1).
-- Hrm.Employees: تعدادِ فرزندِ مشمول + سابقهٔ پیشین (ماه) — برای حق اولاد و پایهٔ سنوات.
-- Hrm.SalarySlips: اجزای تفکیکیِ مزایا (حق‌مسکن/بن/اولاد/سنوات/شب‌کاری/جمعه‌کاری) + سهمِ کارفرما.
-- idempotent؛ توسطِ DatabaseMigratorِ استارت‌آپ هم خودکار اعمال می‌شود.
-- =============================================================================
USE SamaHesab;
GO

-- ── Hrm.Employees ──
IF COL_LENGTH('Hrm.Employees', 'ChildrenCount') IS NULL
    ALTER TABLE Hrm.Employees ADD ChildrenCount int NOT NULL CONSTRAINT DF_Employees_ChildrenCount DEFAULT 0;
GO
IF COL_LENGTH('Hrm.Employees', 'PriorServiceMonths') IS NULL
    ALTER TABLE Hrm.Employees ADD PriorServiceMonths int NOT NULL CONSTRAINT DF_Employees_PriorServiceMonths DEFAULT 0;
GO

-- ── Hrm.SalarySlips — اجزای تفکیکی (همگی decimal(18,2)، پیش‌فرض ۰) ──
IF COL_LENGTH('Hrm.SalarySlips', 'HousingAllowance') IS NULL
    ALTER TABLE Hrm.SalarySlips ADD HousingAllowance decimal(18,2) NOT NULL CONSTRAINT DF_Slip_Housing DEFAULT 0;
GO
IF COL_LENGTH('Hrm.SalarySlips', 'FoodAllowance') IS NULL
    ALTER TABLE Hrm.SalarySlips ADD FoodAllowance decimal(18,2) NOT NULL CONSTRAINT DF_Slip_Food DEFAULT 0;
GO
IF COL_LENGTH('Hrm.SalarySlips', 'ChildAllowance') IS NULL
    ALTER TABLE Hrm.SalarySlips ADD ChildAllowance decimal(18,2) NOT NULL CONSTRAINT DF_Slip_Child DEFAULT 0;
GO
IF COL_LENGTH('Hrm.SalarySlips', 'SeniorityPay') IS NULL
    ALTER TABLE Hrm.SalarySlips ADD SeniorityPay decimal(18,2) NOT NULL CONSTRAINT DF_Slip_Seniority DEFAULT 0;
GO
IF COL_LENGTH('Hrm.SalarySlips', 'NightShiftPay') IS NULL
    ALTER TABLE Hrm.SalarySlips ADD NightShiftPay decimal(18,2) NOT NULL CONSTRAINT DF_Slip_Night DEFAULT 0;
GO
IF COL_LENGTH('Hrm.SalarySlips', 'HolidayPay') IS NULL
    ALTER TABLE Hrm.SalarySlips ADD HolidayPay decimal(18,2) NOT NULL CONSTRAINT DF_Slip_Holiday DEFAULT 0;
GO
IF COL_LENGTH('Hrm.SalarySlips', 'EmployerInsurance') IS NULL
    ALTER TABLE Hrm.SalarySlips ADD EmployerInsurance decimal(18,2) NOT NULL CONSTRAINT DF_Slip_EmpIns DEFAULT 0;
GO
