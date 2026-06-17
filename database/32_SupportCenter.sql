-- =============================================================================
-- 🆘 HC-2 — مرکزِ پشتیبانی و بازخوردِ مشتری (schema Sup)
-- جدول‌ها: گزارشِ باگ، درخواستِ قابلیت، تیکت + پیام، دانشنامه (کَش)، یادداشتِ نسخه، نشستِ ریموت.
-- ⚠️ این جدول‌ها فقط دادهٔ پشتیبانی نگه می‌دارند — هیچ دادهٔ مالی/فاکتور/تراکنش.
-- idempotent؛ توسطِ DatabaseMigratorِ استارت‌آپ خودکار اعمال می‌شود.
-- =============================================================================
USE SamaHesab;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Sup')
    EXEC('CREATE SCHEMA Sup');
GO

-- ─── گزارشِ باگ ──────────────────────────────────────────────────────────────
IF OBJECT_ID('Sup.BugReports', 'U') IS NULL
CREATE TABLE Sup.BugReports (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId        INT NOT NULL,
    Title            NVARCHAR(200) NOT NULL,
    Description      NVARCHAR(MAX) NOT NULL,
    Severity         INT NOT NULL DEFAULT 1,   -- 0=Low 1=Medium 2=High 3=Critical
    Category         INT NOT NULL DEFAULT 10,  -- نگاه: SupportCategory
    ExpectedResult   NVARCHAR(MAX) NULL,
    ActualResult     NVARCHAR(MAX) NULL,
    StepsToReproduce NVARCHAR(MAX) NULL,
    DiagnosticsJson  NVARCHAR(MAX) NULL,
    ScreenName       NVARCHAR(120) NULL,
    AttachmentPath   NVARCHAR(400) NULL,
    Status           INT NOT NULL DEFAULT 0,   -- SupportStatus
    Sync             INT NOT NULL DEFAULT 0,   -- 0=Local 1=Queued 2=Synced 3=Failed
    RemoteId         NVARCHAR(60) NULL,
    SyncedAt         DATETIME2 NULL,
    CreatedAt        DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt        DATETIME2 NULL
);
GO
IF OBJECT_ID('Sup.BugReports','U') IS NOT NULL AND NOT EXISTS
   (SELECT 1 FROM sys.indexes WHERE name='IX_BugReports_Company' AND object_id=OBJECT_ID('Sup.BugReports'))
    CREATE INDEX IX_BugReports_Company ON Sup.BugReports(CompanyId, Status);
GO

-- ─── درخواستِ قابلیت ─────────────────────────────────────────────────────────
IF OBJECT_ID('Sup.FeatureRequests', 'U') IS NULL
CREATE TABLE Sup.FeatureRequests (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId        INT NOT NULL,
    Title            NVARCHAR(200) NOT NULL,
    Description      NVARCHAR(MAX) NOT NULL,
    BusinessBenefit  NVARCHAR(MAX) NULL,
    Priority         INT NOT NULL DEFAULT 1,   -- 0=Low 1=Medium 2=High
    AttachmentPath   NVARCHAR(400) NULL,
    Status           INT NOT NULL DEFAULT 0,
    VoteCount        INT NOT NULL DEFAULT 0,
    Sync             INT NOT NULL DEFAULT 0,
    RemoteId         NVARCHAR(60) NULL,
    SyncedAt         DATETIME2 NULL,
    CreatedAt        DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt        DATETIME2 NULL
);
GO

-- ─── تیکتِ پشتیبانی + پیام‌ها ─────────────────────────────────────────────────
IF OBJECT_ID('Sup.SupportTickets', 'U') IS NULL
CREATE TABLE Sup.SupportTickets (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId      INT NOT NULL,
    Subject        NVARCHAR(200) NOT NULL,
    Body           NVARCHAR(MAX) NOT NULL,
    Category       INT NOT NULL DEFAULT 10,
    Status         INT NOT NULL DEFAULT 0,
    LastActivityAt DATETIME2 NULL,
    Sync           INT NOT NULL DEFAULT 0,
    RemoteId       NVARCHAR(60) NULL,
    SyncedAt       DATETIME2 NULL,
    CreatedAt      DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt      DATETIME2 NULL
);
GO
IF OBJECT_ID('Sup.TicketMessages', 'U') IS NULL
CREATE TABLE Sup.TicketMessages (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    TicketId       INT NOT NULL REFERENCES Sup.SupportTickets(Id),
    Author         NVARCHAR(120) NULL,
    Text           NVARCHAR(MAX) NOT NULL,
    FromSupport    BIT NOT NULL DEFAULT 0,
    SentAt         DATETIME2 NOT NULL DEFAULT GETDATE(),
    AttachmentPath NVARCHAR(400) NULL
);
GO

-- ─── دانشنامه (کَش از وردپرس) ─────────────────────────────────────────────────
IF OBJECT_ID('Sup.KnowledgeArticles', 'U') IS NULL
CREATE TABLE Sup.KnowledgeArticles (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL,
    RemoteId    NVARCHAR(60) NOT NULL,
    Title       NVARCHAR(250) NOT NULL,
    Category    INT NOT NULL DEFAULT 10,
    Summary     NVARCHAR(MAX) NULL,
    Body        NVARCHAR(MAX) NULL,
    Url         NVARCHAR(500) NULL,
    Kind        NVARCHAR(20) NOT NULL DEFAULT 'article',
    PublishedAt DATETIME2 NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt   DATETIME2 NULL
);
GO
IF OBJECT_ID('Sup.KnowledgeArticles','U') IS NOT NULL AND NOT EXISTS
   (SELECT 1 FROM sys.indexes WHERE name='UX_KbArticles_Remote' AND object_id=OBJECT_ID('Sup.KnowledgeArticles'))
    CREATE UNIQUE INDEX UX_KbArticles_Remote ON Sup.KnowledgeArticles(CompanyId, RemoteId);
GO

-- ─── یادداشت‌های نسخه (کَش از وردپرس) ─────────────────────────────────────────
IF OBJECT_ID('Sup.ReleaseNotes', 'U') IS NULL
CREATE TABLE Sup.ReleaseNotes (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL,
    RemoteId    NVARCHAR(60) NOT NULL,
    Version     NVARCHAR(40) NOT NULL,
    Highlights  NVARCHAR(MAX) NULL,
    BugFixes    NVARCHAR(MAX) NULL,
    KnownIssues NVARCHAR(MAX) NULL,
    PublishedAt DATETIME2 NULL,
    IsCurrent   BIT NOT NULL DEFAULT 0,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt   DATETIME2 NULL
);
GO
IF OBJECT_ID('Sup.ReleaseNotes','U') IS NOT NULL AND NOT EXISTS
   (SELECT 1 FROM sys.indexes WHERE name='UX_ReleaseNotes_Remote' AND object_id=OBJECT_ID('Sup.ReleaseNotes'))
    CREATE UNIQUE INDEX UX_ReleaseNotes_Remote ON Sup.ReleaseNotes(CompanyId, RemoteId);
GO

-- ─── نشستِ پشتیبانیِ ریموت ────────────────────────────────────────────────────
IF OBJECT_ID('Sup.RemoteSupportSessions', 'U') IS NULL
CREATE TABLE Sup.RemoteSupportSessions (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL,
    Code        NVARCHAR(40) NOT NULL,
    Status      INT NOT NULL DEFAULT 0,   -- 0=Pending 1=Active 2=Ended 3=Expired
    RequestedBy NVARCHAR(120) NULL,
    Note        NVARCHAR(MAX) NULL,
    StartedAt   DATETIME2 NULL,
    EndedAt     DATETIME2 NULL,
    ExpiresAt   DATETIME2 NOT NULL,
    LogPath     NVARCHAR(400) NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt   DATETIME2 NULL
);
GO
IF OBJECT_ID('Sup.RemoteSupportSessions','U') IS NOT NULL AND NOT EXISTS
   (SELECT 1 FROM sys.indexes WHERE name='UX_RemoteSessions_Code' AND object_id=OBJECT_ID('Sup.RemoteSupportSessions'))
    CREATE UNIQUE INDEX UX_RemoteSessions_Code ON Sup.RemoteSupportSessions(CompanyId, Code);
GO

PRINT N'HC-2: schema Sup + جدول‌های مرکزِ پشتیبانی ensured.';
GO
