# ✈️ ماژولِ گردشگریِ کامل (فروشندهٔ بلیطِ محلی، مدلِ ودیعهٔ تأمین‌کننده) — تحلیل، تقسیم، هماهنگیِ C1/C2

> به‌خواستِ کاربر (@2026-06-22). **HR/حسابداری/خزانه لِینِ C1**؛ Tourism لِینِ C2 بود ولی این ماژول
> عمیقاً به GL/خزانه/حقوق گره می‌خورد ⇒ **C1 رهبریِ دامنه/اسکیمـا/حسابداری/پلِ‌حقوق** را دارد،
> **C2 موتورها/گزارش/UX**. هماهنگی فقط از طریقِ git.

## 🔎 یافته‌های recon (واقعیتِ ریپو — مبنای طراحی)
1. **Tourism موجود است:** `ModuleDef(Tourism,"گردشگری",false,"Airplane")` در `ModuleService.cs` + `GenerateTourismVoucherCommand` + `TourismSettlement` + enum `TourismAccountRole`. مدلِ فعلی **پرداختنیِ تأمین‌کننده** است (نه ودیعه) و هیچ VM/صفحه ندارد. ⇒ **گسترش، نه تکرار.**
2. **VoucherItem فاقدِ `PartyId` است** (فقط AccountId/CostCenter/Project/Currency/Check). سیستمِ فعلی **معینِ طرف‌حساب در GL ندارد** (دریافتنی همه در `1-03-001`؛ `Party.AccountId` در سندسازی استفاده نمی‌شود).
3. **`CreatePaymentCommand(BranchId,FiscalYearId,Date,SupplierId,Amount,PaymentMethod,Description)`** بد را به پرداختنیِ تأمین‌کننده می‌زند؛ حسابِ بدِ دلخواه نمی‌گیرد.
4. **حقوق:** `RunMonthlyPayrollCommand` فیش می‌سازد؛ `FullPayrollInput.OtherEarnings` (مشمولِ بیمه) نقطهٔ تزریقِ پورسانت است.

## 🧭 تصمیم‌های معماری (به‌دلیلِ یافته‌ها)
- **ردیابیِ ودیعهٔ هر تأمین‌کننده در جداولِ `Tur`** (شارژها − برداشت‌ها)؛ در **GL یک حسابِ کنترلیِ واحدِ «ودیعه نزد تأمین‌کننده»** (asset، AccountId از تنظیمات). گزارشِ ماندهٔ هر تأمین‌کننده از `Tur`.
- **شارژِ ودیعه = کامندِ اختصاصی** که سندِ خودش را می‌زند: `Dr ودیعه(کنترلی) / Cr بانک‌یا‌نقد(بر اساسِ روش)`. مغایرت‌گیریِ بانکی سالم می‌ماند چون حسابِ بانک عادی بستانکار می‌شود.
- **همهٔ AccountIdهای کنترلی از یک صفحهٔ تنظیمات** (هم‌الگوی `PayrollSetting`): `TourismSetting` با حساب‌های نقد/دریافتنی/درآمد/COGS/ودیعه/تخفیف/اختلافِ‌ودیعه/هزینهٔ‌پورسانت/پرداختنیِ‌فروشنده + پرچم‌ها (مبنای‌پورسانت‌بعدازتخفیف، آستانهٔ‌ودیعهٔ‌پایین، per-sale-vs-daily، پورسانت‌از‌حقوق‌یا‌مستقل).
- **سندِ هر فروش (per-sale پیش‌فرض):** Dr نقد/دریافتنی + Dr تخفیف(contra) / Cr درآمد ‖ Dr COGS / Cr ودیعهٔ‌کنترلی. (برداشتِ ودیعه، نه پرداختنی.) per-supplier drawdown در جدولِ `Tur` ثبت می‌شود.

## 🗂 تقسیمِ کار (لِین‌ها)

### 🖥 C1 (دامنه/اسکیمـا/حسابداری/خزانه/پلِ‌حقوق)
- **TUR-C1-1** موجودیت‌های `Tur`: `ProductGroup`, `TourismProduct`, `SupplierDeposit`, `TourismSale`+`TourismSaleLine`, `SalePassenger`, `SupplierDailyReport`(+lines), `CommissionRule`, `SalesCommissionEntry`, `TourismSetting` + EF map (multi-tenant/branch) + migrationِ idempotent (`NN_Tourism.sql`, schema `Tur`).
- **TUR-C1-2** تنظیمات: `Get/SaveTourismSettingsCommand` (نگاشتِ حساب‌ها + پرچم‌ها) — هم‌الگوی PayrollSettings.
- **TUR-C1-3** ثبتِ فروش + **سندِ متوازنِ خودکار** (`CreateTourismSaleCommand` → الگوی `TryCreateSalesVoucher`؛ COGS↔ودیعهٔ‌کنترلی) + ثبتِ drawdownِ per-supplier + مسافران.
- **TUR-C1-4** ودیعه: `TopUpSupplierDepositCommand` (سندِ Dr ودیعه/Cr بانک) + کوئریِ **ماندهٔ ودیعهٔ هر تأمین‌کننده** + آلارمِ کم‌بودنِ ودیعه.
- **TUR-C1-5** گزارشِ روزانهٔ تأمین‌کننده + **آشتی (Reconcile)**: ثبتِ `SupplierDeductedAmount`؛ اختلاف → سندِ تعدیل (ودیعه ↔ حسابِ اختلاف).
- **TUR-C1-6** پلِ حقوق: کوئریِ جمعِ پورسانتِ ماه per-employee → تزریقِ idempotent به `RunMonthlyPayrollCommand` (`OtherEarnings`/پرچم).

### 💻 C2 (موتورها + گزارش + UX)
- **TUR-C2-1** موتورِ خالصِ **پورسانت** (`CommissionEngine`): سه مبنا (PerUnit/PercentOfSale/PercentOfProfit) + قبل/بعدِ تخفیف + ترتیبِ resolve (محصول>گروه>پیش‌فرض) + تست. بدونِ DB.
- **TUR-C2-2** موتورِ خالصِ **سود/برداشتِ ودیعه** (محاسبهٔ خطوط/سرجمع برای سند و گزارش) + تست.
- **TUR-C2-3** گزارش‌ها (IReportService): ماندهٔ ودیعهٔ تأمین‌کننده، گزارشِ روزانه با لیستِ مسافر، سودِ محصول/فروش، پورسانتِ ماهانهٔ فروشنده، عملکردِ فروشنده.
- **TUR-C2-4** UX (WPF/MVVM، RTL): کاتالوگِ محصول، شارژِ ودیعه + ماندهٔ زنده، صفحهٔ فروش (سود+پورسانتِ زنده، مسافران، فروشنده)، گزارشِ روزانه+آشتی، قواعدِ پورسانت، تنظیمات. + گیتِ منو بر `ModuleService.IsEnabled("Tourism")`.
- **TUR-C2-5** دمو فارسی در `RunDemoDataAsync` (۳ تأمین‌کننده با ودیعهٔ اولیه، محصولات از جمله «گشت دور جزیره» با لیستِ مسافر، ۲ فروشنده با قراردادِ متفاوت، چند فروش، یک گزارشِ روزانه).

> **ترتیب:** C2 موتورهای خالص (TUR-C2-1/2) را موازی می‌زند تا قرارداد روشن شود؛ C1 اسکیمـا+تنظیمات+فروش/ودیعه (TUR-C1-1..4) را. سپس آشتی (C1-5)، پلِ‌حقوق (C1-6)، گزارش/UX/دمو (C2-3/4/5).
> **اسکیمـا:** migrationِ raw-SQLِ شماره‌دارِ بعدی در `database/` (idempotent، GO-split، بدونِ USE)، EmbeddedResource، schema `Tur`، reuse از Acc/Crm/Hrm.
