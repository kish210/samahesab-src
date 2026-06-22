-- =============================================================================
-- ATT-C1-1 — اسکیمـای ماژولِ حضوروغیاب: شیفت/تقویمِ تعطیلات/درخواستِ مرخصی.
-- idempotent؛ توسطِ DatabaseMigratorِ استارت‌آپ هم خودکار اعمال می‌شود.
-- =============================================================================
USE SamaHesab;
GO

-- ── Hrm.Shifts — شیفتِ کاری ──
IF OBJECT_ID('Hrm.Shifts', 'U') IS NULL
BEGIN
    CREATE TABLE Hrm.Shifts (
        Id            int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CompanyId     int           NOT NULL,
        Name          nvarchar(100) NOT NULL,
        StartTime     time          NOT NULL CONSTRAINT DF_Shift_Start DEFAULT '08:00',
        EndTime       time          NOT NULL CONSTRAINT DF_Shift_End   DEFAULT '16:00',
        BreakMinutes  int           NOT NULL CONSTRAINT DF_Shift_Break DEFAULT 0,
        IsNight       bit           NOT NULL CONSTRAINT DF_Shift_Night DEFAULT 0,
        StandardHours decimal(9,2)  NOT NULL CONSTRAINT DF_Shift_Std   DEFAULT 7.33,
        IsActive      bit           NOT NULL CONSTRAINT DF_Shift_Active DEFAULT 1,
        Notes         nvarchar(500) NULL
    );
END
GO

-- ── Hrm.Holidays — تقویمِ تعطیلات ──
IF OBJECT_ID('Hrm.Holidays', 'U') IS NULL
BEGIN
    CREATE TABLE Hrm.Holidays (
        Id         int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CompanyId  int           NOT NULL,
        Date       nvarchar(10)  NOT NULL,
        Title      nvarchar(200) NOT NULL CONSTRAINT DF_Holiday_Title DEFAULT N'تعطیل',
        IsOfficial bit           NOT NULL CONSTRAINT DF_Holiday_Off    DEFAULT 1,
        CONSTRAINT UQ_Holidays_Company_Date UNIQUE (CompanyId, Date)
    );
END
GO

-- ── Hrm.LeaveRequests — درخواستِ مرخصی ──
IF OBJECT_ID('Hrm.LeaveRequests', 'U') IS NULL
BEGIN
    CREATE TABLE Hrm.LeaveRequests (
        Id           int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CompanyId    int           NOT NULL,
        EmployeeId   int           NOT NULL,
        LeaveType    nvarchar(30)  NOT NULL CONSTRAINT DF_Leave_Type   DEFAULT N'استحقاقی',
        StartDate    nvarchar(10)  NOT NULL,
        EndDate      nvarchar(10)  NOT NULL,
        Days         decimal(9,2)  NOT NULL CONSTRAINT DF_Leave_Days   DEFAULT 0,
        Hours        decimal(9,2)  NOT NULL CONSTRAINT DF_Leave_Hours  DEFAULT 0,
        Status       nvarchar(20)  NOT NULL CONSTRAINT DF_Leave_Status DEFAULT N'درخواست',
        Reason       nvarchar(500) NULL,
        DecidedBy    int           NULL,
        DecisionDate nvarchar(10)  NULL,
        DecisionNote nvarchar(500) NULL
    );
    CREATE INDEX IX_LeaveRequests_Company_Emp ON Hrm.LeaveRequests (CompanyId, EmployeeId);
END
GO

-- ستون‌های ممیزیِ AuditableEntity (EF لازم دارد) — idempotent برای هر سه جدول.
IF COL_LENGTH('Hrm.Shifts', 'CreatedAt') IS NULL
    ALTER TABLE Hrm.Shifts ADD CreatedAt datetime NOT NULL CONSTRAINT DF_Shift_Created DEFAULT GETDATE();
GO
IF COL_LENGTH('Hrm.Shifts', 'UpdatedAt') IS NULL ALTER TABLE Hrm.Shifts ADD UpdatedAt datetime NULL;
GO
IF COL_LENGTH('Hrm.Holidays', 'CreatedAt') IS NULL
    ALTER TABLE Hrm.Holidays ADD CreatedAt datetime NOT NULL CONSTRAINT DF_Holiday_Created DEFAULT GETDATE();
GO
IF COL_LENGTH('Hrm.Holidays', 'UpdatedAt') IS NULL ALTER TABLE Hrm.Holidays ADD UpdatedAt datetime NULL;
GO
IF COL_LENGTH('Hrm.LeaveRequests', 'CreatedAt') IS NULL
    ALTER TABLE Hrm.LeaveRequests ADD CreatedAt datetime NOT NULL CONSTRAINT DF_Leave_Created DEFAULT GETDATE();
GO
IF COL_LENGTH('Hrm.LeaveRequests', 'UpdatedAt') IS NULL ALTER TABLE Hrm.LeaveRequests ADD UpdatedAt datetime NULL;
GO
