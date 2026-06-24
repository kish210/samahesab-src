# SamaHesab — راهنمای هماهنگیِ کلود (هر دو ماشین: PC و لپ‌تاپ)

> این فایل در git است و در شروعِ هر نشست خوانده می‌شود. هدف: **خروجیِ یکسان** روی PC و لپ‌تاپ.

## واقعیتِ معماری (مهم)
هر ماشین **DB محلیِ خودش** را دارد (`Server=.\SQLEXPRESS;Database=SamaHesab`).
`BaseUrl=kishwifi.com` فقط سرورِ **پشتیبانیِ** وردپرس است، نه API دادهٔ مشترک.
⇒ **تنها کانالِ مشترکِ دو ماشین `git` است.** هر چیزی که باید بین دو ماشین یکسان بماند
(کد، نسخه، پیش‌فرضِ ماژول/منو) باید **در git** باشد — نه در DB، نه در `%AppData%\SamaHesab\`.

## مخازن (Repos) — تفکیکِ سورس از خروجی (@2026-06-20)
- 🔒 **`kish210/samahesab-src`** (خصوصی) = **سورس‌کد**. توسعهٔ مشترکِ PC/لپ‌تاپ **فقط اینجا**. mohammad9381 با دسترسیِ Write دعوت شده (باید Accept کند).
- 🌍 **`kish210/SamaHesab`** (public) = **فقط فایل‌های کامپایل‌شده/release برای کاربر**. سایت + UpdateServiceِ برنامه آخرین release را از همین‌جا می‌خوانند → باید public بماند.
- **تکمیلِ کات‌اوور (هر ماشین یک‌بار):** `git remote set-url origin https://github.com/kish210/samahesab-src.git`
  از آن پس: **سورس فقط به samahesab-src** · **نصاب‌ها/release فقط روی SamaHesab** (با `gh release ... -R kish210/SamaHesab`). تا قبل از کات‌اوورِ هر دو ماشین، origin همان SamaHesab است.

## چرخهٔ کارِ اجباری (هر نشست، هر دو نمونه رعایت کنند)
1. **شروع:** `git pull --rebase` بزن، بعد **کلِ `todo.rm` را بخوان**.
   - موردی که `[x]`/`[~]` یا claimِ ماشینِ دیگر است → دست نزن.
2. **قبل از کدنویسی:** مورد را در `todo.rm` با تگِ ماشین claim کن (`[~] 🚧 pc@…` یا `laptop@…`) و **همان را push کن**.
3. **بعد از هر کار:** `[x]` + خلاصهٔ یک‌خطی، **build سبز + ۴۰۵ تست**، سپس commit و **push**.
4. **قبل از هر push:** `git fetch && rebase origin/main`؛ تعارض → نسخهٔ صاحبِ همان lane مقدم است.
5. **بعد از کار:** دوباره `todo.rm` را برای تعارض چک کن.

## قواعدِ ثابت
- **هرگز** دو ماشین هم‌زمان یک فایل را ویرایش نکنند (قبل از کد، در todo.rm claim کنید).
- **منو/ماژول:** پیش‌فرض فقط در کد (`ModuleService.Load`) — در git. فایلِ
  `%AppData%\SamaHesab\modules.json` فقط override محلی است؛ برای یکسانی روی هیچ ماشینی نگهش ندارید
  (یا با «تنظیمات → مدیریت ماژول‌ها → خروجی/ورودیِ تنظیمات» یکسانش کنید).
- **نسخه‌بندی:** فقط در `Directory.Build.props` + سه `installer/*.iss` (هر چهار برابر).
  نصاب‌ها (Setup.exe) با **Inno Setup/ISCC فقط روی لپ‌تاپ** ساخته و به GitHub Release آپلود می‌شوند.
- **زبان:** پاسخ‌ها به کاربر فارسی.
- **footerِ commit:** `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- push: `git -c http.version=HTTP/1.1 push origin main`

## lane ها (مالکیت برای جلوگیری از تداخل)
- **pc:** حسابداری/خزانه/گزارشِ مالی/BI/امنیت/زیرساخت/HR
- **laptop:** فروش/خرید/POS/رستوران/انبار/مشتری/اعتبارسنجی/UXِ عملیاتی

## ماژولارسازی (Monolith → Modular ERP Platform) — قواعدِ الزام‌آور
> مرجعِ کامل: [`docs/MODULARIZATION_PLAN.md`](docs/MODULARIZATION_PLAN.md). **استخراجِ کنترل‌شده است، نه بازنویسی.**

**تعریفِ هسته (فقط این‌ها در Core می‌مانند):** حسابداری · خزانه · انبار · فروش · خرید · اشخاص(پایه) · امنیت/مجوز/کاربر · چندشعبه · هستهٔ گزارش · موتورِ قالب · لایسنس · تنظیمات · لاگِ حسابرسی · زیرساختِ اعلان.
**ماژول‌ها (اختیاری/قابلِ‌حذف):** POS · رستوران · گردشگری · HR/حقوق/حضوروغیاب · CRM(باشگاه) · هتل · پیمانکاری · پشتیبانی.

**ناقض‌ناپذیرها (هر دو ماشین، هر PR):**
1. **هسته هرگز به پیاده‌سازیِ ماژول وابسته نیست** — فقط به `SamaHesab.Modules.Abstractions` (`IModule`). ماژول‌ها به Core + Abstractions وابسته‌اند، نه برعکس. ارتباط فقط با اینترفیس‌های Core (`ICurrentUserService`/`IRepository<>`/`IMediator`) + رویداد.
2. **ماژول خودش را ثبت می‌کند** — منو/مجوز/گزارش/ویجت/سرویس/مپِ EF از طریقِ `IModule`. هیچ hard-code در Core. (G4: `ApplicationDbContext.OnModelCreating` فقط `module.ConfigureModel` ماژول‌های فعال را صدا می‌زند.)
3. **removability** — با حذف/غیرفعال‌کردنِ ماژول: هسته کار کند، **بدونِ** فسادِ DB، منوی شکسته، گزارشِ شکسته، یا مجوزِ شکسته. دادهٔ تراکنشیِ تاریخی حفظ می‌شود (drop فقط با تأییدِ صریحِ کاربر). Core هرگز به schemaی ماژول JOIN نمی‌زند.
4. **DB:** جدولِ هر ماژول در schemaی خودش؛ مهاجرتش از `IModule.GetMigrationScripts()`.
5. **بسته‌بندیِ ماژول:** هر ماژول → `ModuleName.dll` + `module.json` (Key/Name/Version/schema/dependencies) + `version.json` + `icon.png`؛ بستهٔ استقرار `<Module>.mspkg`. نصب بدونِ rebuildِ Core.
6. **لایسنسِ per-module** — Loader پیش از بارگذاری وضعیتِ لایسنسِ ماژول را از `LicenseService` می‌پرسد (بسته‌ها: «فقط حسابداری»، «حسابداری+POS»، …).
7. **انضباطِ اجرا:** هر ماژول = یک claim در `todo.rm` + یک PR/بازهٔ کوتاه. ماژولِ لِینِ طرفِ مقابل فقط با هماهنگی. در هر فاز: build سبز + کلِ تست‌ها سبز + **تستِ removability**.

**تقسیمِ کارِ فازها @2026-06-24:**
- **انجام‌شده (laptop):** پروژهٔ `SamaHesab.Modules.Abstractions` + قراردادِ `IModule` + قلابِ `ApplicationDbContext` برای دریافتِ `IEnumerable<IModule>`.
- **فاز ۰ بقیه (🖥 pc/پلتفرم):** `ModuleLoader` (کشف+بارگذاریِ ماژولِ فعال) + ارتقای `ModuleService`→Manager (نصب/فعال/غیرفعال/حذف/لایسنس) + صداکردنِ `ConfigureModel` فقط برای ماژول‌های فعال در `OnModelCreating`.
- **فاز ۱ — پایلوتِ Hotel (🖥 pc):** کوچک‌ترین ماژول و مالِ pc → الگوی مرجعِ removability.
- **فاز ۲ — ماژول‌های C2 (💻 laptop):** Tourism → Contracting → Restaurant → POS. *نکته:* بک‌اندِ گردشگری مالِ pc است؛ استخراجش `Domain/Application/Tourism` را جابه‌جا می‌کند ⇒ laptop قبل از استخراجِ گردشگری از pc claim بگیرد.
- **فاز ۳ — ماژول‌های C1 (🖥 pc):** HR/Payroll/Attendance · CRM(باشگاه/امتیاز).
- **ریسکِ مشترک G4 (طراحیِ دونفره):** مدلِ شرطیِ EF — موجودیتِ ماژولِ غیرفعال نباید مپ شود.

> جزئیاتِ بیشتر و backlogِ شماره‌دارِ کارهای باقی‌مانده در انتهای `todo.rm`.
