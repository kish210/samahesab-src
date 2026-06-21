-- 38_PartyGroupBirthDate — افزودنِ گروهِ مشتری و تاریخِ تولد به طرف‌حساب (UX-CRM-FORM).
-- این فیلدها در فرمِ مشتری بودند ولی ستون نداشتند و ذخیره نمی‌شدند. idempotent.

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE [object_id] = OBJECT_ID('Crm.Parties') AND [name] = 'GroupId')
    ALTER TABLE Crm.Parties ADD GroupId INT NULL;            -- گروهِ مشتری (عادی/طلایی/عمده)

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE [object_id] = OBJECT_ID('Crm.Parties') AND [name] = 'BirthDate')
    ALTER TABLE Crm.Parties ADD BirthDate NVARCHAR(20) NULL;  -- تاریخِ تولد (شمسی، رشته‌ای)
