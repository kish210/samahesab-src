# 🏗 ماژولِ پیمانکاری (Peymankari/Contracting) — تحلیل، تقسیم، هماهنگیِ C1/C2

> به‌خواستِ کاربر (@2026-06-22). پیمانکار به **کارفرما** از طریقِ **صورت‌وضعیت** فاکتور می‌دهد.
> حسابداری/خزانه/تنظیمات لِینِ C1؛ موتورِ خالص + UI + گزارش + دمو لِینِ C2. هماهنگی فقط از طریقِ git.
> **STEP 0 است — منتظرِ تأییدِ مدل/مَپینگ توسطِ کاربر؛ کد هنوز نوشته نشده.**

## 🔎 یافته‌های recon (واقعیتِ ریپو)
1. کلیدِ `Contracting` در ModuleService **نیست** → افزوده می‌شود ("پیمانکاری").
2. `VoucherItemDto` **`ProjectId` + `CostCenterId` دارد** و `Acc.Project`/`Acc.CostCenter` (CE-1) موجودند ⇒ **بُعدِ پروژهٔ موجود reuse می‌شود** (خطِ درآمد/هزینه با `ProjectId`). **PartyId روی خطِ سند نیست.**
3. `CreateReceiptCommand(Branch,FY,Date,CustomerId,Amount,Method,Desc)` بس را به **دریافتنیِ مشتری** می‌زند (حسابِ ثابت)؛ حسابِ بسِ دلخواه نمی‌گیرد.
4. `FinancialReportsQueries` فیلترِ `ProjectId/CostCenterId` دارد (گزارشِ پروژه reuse می‌شود).

## 🧭 تصمیم‌های معماری
- **بُعدِ پروژه:** `Con.Project(پیمان)` فیلدِ `ProjectDimensionId` دارد که به `Acc.Project.Id` اشاره می‌کند؛ خطوطِ درآمد/هزینه با همان `VoucherItemDto.ProjectId` تگ می‌شوند (سود/زیانِ پروژه از گزارش‌های موجود).
- **نبودِ PartyId روی خطِ سند:** دریافتنیِ کارفرما / بدهیِ پیش‌پرداخت / سپرده‌های حسن‌انجام‌و‌بیمه = **حساب‌های کنترلیِ واحد (AccountId از تنظیمات)**؛ ردیابیِ per-employer/per-project از جداولِ `Con` (Project→EmployerPartyId، صورت‌وضعیت/پیش‌پرداخت/سپرده با ProjectId).
- **خزانه:** وصولِ خالص = `CreateReceiptCommand` (Cr دریافتنی). **پیش‌پرداختِ دریافتی** و **آزادسازیِ سپردهٔ حسن‌انجام/بیمه** = کامندهای اختصاصیِ خودشان (Dr بانک / Cr حسابِ مشخص از تنظیمات) — مغایرت‌گیریِ بانکی سالم.
- **همهٔ درصدها و AccountIdها از تنظیمات** (پیش‌فرضِ سراسری + override به‌ازای هر پروژه). هیچ نرخ/حساب هاردکد نمی‌شود.

## 💧 آبشارِ صورت‌وضعیت (دقیق، به‌ترتیب)
```
PeriodWork      = CumulativeGrossWork − PreviousCumulative
GrossThisPeriod = PeriodWork + AdjustmentAmount + MaterialDiffAmount
AdvanceRecovery = AdvancePercent × PeriodWork        (سقف: کلِ بازیافت ≤ پیش‌پرداختِ دریافتی)
Retention       = RetentionPercent × GrossThisPeriod
Insurance       = InsuranceWithholdPercent × GrossThisPeriod
Tax             = TaxWithholdPercent × GrossThisPeriod
Penalty/Other   = ورودی
NetPayable      = GrossThisPeriod − AdvanceRecovery − Retention − Insurance − Tax − Penalty − Other
```

## 🧾 مَپینگِ حسابداری (صورت‌وضعیتِ موقت، Approved→Posted، یک سندِ متوازن)
```
Dr دریافتنیِ کارفرما           = NetPayable
Dr سپردهٔ حسن‌انجام‌کار (دارایی) = Retention
Dr سپردهٔ بیمه (دارایی)         = Insurance
Dr پیش‌پرداختِ مالیات (دارایی)  = Tax
Dr بدهیِ پیش‌پرداختِ کارفرما    = AdvanceRecovery   (بازیافتِ پیش‌پرداخت، بدهی را کم می‌کند)
Dr هزینهٔ جریمه                = Penalty + Other
Cr درآمدِ پیمان (تگِ پروژه)     = GrossThisPeriod
```
(تراز: Σ بد = NetPayable+Retention+Insurance+Tax+AdvanceRecovery+Penalty+Other = GrossThisPeriod = بستانکار.)
- پیش‌پرداختِ اولیه: Dr بانک / Cr بدهیِ پیش‌پرداخت (کامندِ اختصاصی).
- وصولِ خالص: Dr بانک / Cr دریافتنی (`CreateReceiptCommand`).
- آزادسازیِ حسن‌انجام/بیمه (بعدِ مفاصاحساب): Dr بانک / Cr داراییِ سپرده (کامندِ اختصاصی).

## 🗂 تقسیمِ کار (لِین‌ها)

### 🖥 C1 (دامنه/اسکیمـا/حسابداری/خزانه/تنظیمات/گزارش‌داده)
- **CON-C1-1** موجودیت‌های `Con`: `Project`, `ProgressStatement`, `StatementDeduction`, `AdvancePayment`, `Guarantee` (+ enumها) + EF map (tenant/branch) + migrationِ idempotent (`NN_Contracting.sql`, schema `Con`).
- **CON-C1-2** تنظیمات: نگاشتِ حساب‌ها به‌ازای نوعِ کسر + درصدهای پیش‌فرضِ سراسری + override به‌ازای پروژه (هم‌الگوی PayrollSetting). انتخابِ بُعدِ پروژه.
- **CON-C1-3** ثبتِ صورت‌وضعیت + **سندِ متوازنِ Approve→Post** (با اعدادِ موتورِ CON-C2-1) + تگِ پروژه روی درآمد.
- **CON-C1-4** پیش‌پرداخت: کامندِ دریافتِ پیش‌پرداخت (Dr بانک/Cr بدهی) + بازیافتِ سقف‌دار در صورت‌وضعیت + کوئریِ ماندهٔ پیش‌پرداخت.
- **CON-C1-5** ضمانت‌نامه‌ها + آزادسازیِ سپردهٔ حسن‌انجام/بیمه (کامندِ Dr بانک/Cr سپرده) + آلارمِ انقضا.
- **CON-C1-6** کوئری‌های سود/زیانِ پروژه + درصدِ پیشرفتِ مالی + گزارشِ سپرده‌ها/پیش‌پرداخت (داده؛ رندر با C2).

### 💻 C2 (موتورِ خالص + UI + گزارش + دمو + رجیستری)
- **CON-C2-1** **`StatementWaterfallEngine`ِ خالص** (محاسبهٔ آبشار + سقفِ بازیافت) + تستِ جامع. بدونِ DB.
- **CON-C2-2** UI (WPF/RTL): لیست/مَستر پروژه، ورودِ صورت‌وضعیت (آبشارِ زنده)، پیش‌پرداخت، ثبتِ ضمانت‌نامه، تنظیمات، داشبوردِ پروژه.
- **CON-C2-3** گزارش‌ها (IReportService): چاپِ رسمیِ صورت‌وضعیت + خلاصهٔ مالیِ پروژه + سپرده‌ها + پیش‌پرداخت + ضمانت‌نامه + سود/زیان/پیشرفت.
- **CON-C2-4** دمو فارسی در `RunDemoDataAsync` (یک کارفرما + یک پیمانِ فهرست‌بها با پیش‌پرداخت و ضمانت‌نامه + دو صورت‌وضعیتِ موقت + چند هزینهٔ پروژه).
- **CON-C2-5** رجیستری: `ModuleDef(Contracting,"پیمانکاری",…)` در OptionalModules + گیتِ منو. (با افزودن به OptionalModules خودکار در **ویزاردِ انتخابِ نصب** ظاهر می‌شود.)

## 🧩 «انتخابِ ماژول موقعِ نصب» (درخواستِ دوم)
- **از قبل کار می‌کند:** `FirstRunWizardViewModel` روی `OptionalModules` حلقه می‌زند → همهٔ اختیاری‌ها (و پیمانکاری پس از افزوده‌شدن) موقعِ اولِ اجرا انتخاب‌پذیرند؛ فقط منتخب‌ها فعال می‌شوند (+ کنترلِ تداخلِ TUR-C1-7).
- **تصمیم با کاربر:** اگر چک‌باکسِ Components در خودِ نصاب (Inno Setup) هم خواسته شود → لایهٔ بسته‌بندی/ساختِ نصاب روی لپ‌تاپ؛ به‌صورتِ تسکِ جدا اضافه می‌شود. پیشنهاد: ویزاردِ اولِ اجرا کافی است.

> **ترتیب:** C2 موتورِ خالص (CON-C2-1) را موازی می‌زند؛ C1 اسکیمـا+تنظیمات (CON-C1-1/2). سپس ثبت/سند (C1-3)، پیش‌پرداخت/ضمانت (C1-4/5)، و گزارش/UI/دمو (C2-2/3/4/5).
> **اسکیمـا:** migrationِ raw-SQLِ شماره‌دارِ بعدی، idempotent، GO-split، بدونِ USE، schema `Con`، reuse از Acc/Crm.
