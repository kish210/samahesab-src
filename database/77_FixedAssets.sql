-- 77_FixedAssets.sql — U-FIXED-ASSET: داراییِ ثابت + استهلاک (هم‌راستا با «نرم‌افزار دارایی ثابتِ» راهکاران).
-- چارتِ حساب از قبل حساب‌هایِ «دارایی‌های ثابت» (2-01..2-07) و «استهلاک» (8-03) را دارد؛
-- این جدول فقط رجیستریِ دارایی‌ها + کشِ استهلاکِ انباشته است. سندِ استهلاک به‌صورتِ تجمیعی توسطِ
-- DepreciateFixedAssetsCommand (بدهکارِ 8-03 / بستانکارِ 2-06) زده می‌شود. idempotent.
IF SCHEMA_ID('Acc') IS NULL EXEC('CREATE SCHEMA Acc');
GO

IF OBJECT_ID('Acc.FixedAssets', 'U') IS NULL
BEGIN
    CREATE TABLE Acc.FixedAssets (
        Id                       INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId                INT            NOT NULL,
        Code                     NVARCHAR(50)   NOT NULL,
        Name                     NVARCHAR(200)  NOT NULL,
        Description              NVARCHAR(500)  NULL,
        PurchaseDate             NVARCHAR(20)   NOT NULL,          -- شمسی «yyyy/MM/dd»
        PurchaseCost             DECIMAL(18,2)  NOT NULL,
        SalvageValue             DECIMAL(18,2)  NOT NULL CONSTRAINT DF_FixedAssets_Salvage DEFAULT(0),
        UsefulLifeMonths         INT            NOT NULL,
        Method                   INT            NOT NULL CONSTRAINT DF_FixedAssets_Method  DEFAULT(0),  -- 0=خط مستقیم · 1=نزولی
        IsActive                 BIT            NOT NULL CONSTRAINT DF_FixedAssets_Active  DEFAULT(1),
        AccumulatedDepreciation  DECIMAL(18,2)  NOT NULL CONSTRAINT DF_FixedAssets_Accum   DEFAULT(0),
        LastDepreciatedMonth     NVARCHAR(7)    NULL,             -- «yyyy/MM»
        CreatedAt                DATETIME2      NOT NULL CONSTRAINT DF_FixedAssets_Created DEFAULT(SYSDATETIME()),
        UpdatedAt                DATETIME2      NULL,
        CreatedByUserId          INT            NULL,
        UpdatedByUserId          INT            NULL
    );
    CREATE INDEX IX_FixedAssets_Company ON Acc.FixedAssets(CompanyId);
END
GO
