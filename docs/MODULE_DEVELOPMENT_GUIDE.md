# راهنمای ساختِ ماژول برای سما حساب

> سما حساب یک **پلتفرمِ ERP ماژولار** است: یک **هسته** (Core) ثابت + ماژول‌های **اختیاری و قابلِ‌حذف**.
> این سند می‌گوید یک ماژول باید چه قواعدی را رعایت کند تا **سیستم آن را بشناسد، بارگذاری کند و به‌صورتِ مستقل به‌روزرسانی شود** — بدونِ تغییرِ هسته و بدونِ نصبِ مجددِ کلِ برنامه.

مرجعِ معماری: [`MODULARIZATION_PLAN.md`](MODULARIZATION_PLAN.md) · قواعدِ الزام‌آور: بخشِ «ماژولارسازی» در `CLAUDE.md`.

---

## ۱) قراردادِ شناسایی: `IModule`

تنها چیزی که سیستم از یک ماژول می‌شناسد، اینترفیسِ `IModule` در پروژهٔ `SamaHesab.Modules.Abstractions` است.
هر ماژول **یک کلاسِ عمومیِ بدونِ‌پارامتر** دارد که `IModule` را پیاده می‌کند:

```csharp
public sealed class TourismModule : IModule
{
    public string Key         => "Tourism";        // یکتا؛ مطابقِ کلیدِ ModuleService
    public string DisplayName => "گردشگری";        // نامِ فارسیِ نمایشی
    public string Version     => "1.1.0";          // نسخهٔ مستقلِ ماژول (در «مدیریت ماژول‌ها» دیده می‌شود)

    public void RegisterServices(IServiceCollection s)      // DI + هندلرهای MediatR
        => s.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(TourismModule).Assembly));

    public void ConfigureModel(ModelBuilder b) { /* مپِ EFِ موجودیت‌های ماژول */ }

    public IReadOnlyList<ModuleMenu>       GetMenus()            => new[] { new ModuleMenu("Tourism", "ثبتِ فروش", "TourismSale") };
    public IReadOnlyList<ModulePermission> GetPermissions()     => new[] { new ModulePermission("Tourism", "Sale", "Create", "ثبتِ فروشِ گردشگری") };
    public IReadOnlyList<string>           GetMigrationScripts() => new[] { "51_TourismXyz.sql" };
}
```

| عضو | نقش | قاعده |
|---|---|---|
| `Key` | شناسهٔ یکتا | باید با کلیدِ `ModuleService` برابر باشد؛ ASCII، بدونِ فاصله. |
| `DisplayName` | نامِ فارسی | در منو/صفحهٔ مدیریت دیده می‌شود. |
| `Version` | نسخهٔ ماژول | **با هر تغییرِ ماژول bump شود** (semver). نسخهٔ در حالِ اجرا در «مدیریت ماژول‌ها» نشان داده می‌شود. |
| `RegisterServices` | ثبتِ DI/MediatR | هندلرها را **فقط از اسمبلیِ خودِ ماژول** اسکن کن. |
| `ConfigureModel` | مپِ EF | موجودیت‌های ماژول را در schemaی اختصاصی مپ کن (نه schemaی هسته). |
| `GetMenus` | منوها | فقط وقتی ماژول **نصب+فعال** است نمایش داده می‌شوند. |
| `GetPermissions` | مجوزها | به کاتالوگِ مجوزِ هسته افزوده می‌شوند. |
| `GetMigrationScripts` | مهاجرت‌های DB | نامِ فایل‌های `.sql` نسبت به پوشهٔ `database/`. idempotent باشند. |

---

## ۲) ساختارِ پروژه

- یک پروژهٔ کلاس‌لایبرریِ مستقل: **`SamaHesab.Modules.<Name>`** (مثلِ `SamaHesab.Modules.Tourism`).
- ارجاع‌ها: فقط به **`SamaHesab.Modules.Abstractions`** + اینترفیس‌های هسته (`SamaHesab.Application`/`SamaHesab.Domain` برای `ICurrentUserService`/`IRepository<>`/`IMediator`/رویدادها).
- لایه‌بندیِ داخلی: `Domain/` (موجودیت‌ها) · `Application/` (Command/Query/Handlerِ MediatR) — نام‌فضاها را می‌توان حفظ کرد تا مصرف‌کننده‌ها تغییر نکنند.

---

## ۳) قواعدِ ناقض‌ناپذیر (تا سیستم ماژول را درست بشناسد)

1. **هسته هرگز به ماژول وابسته نیست.** جهتِ وابستگی: `Module → Core + Abstractions`، نه برعکس. هسته صفر `using` به ماژول دارد.
2. **ماژول خودش را ثبت می‌کند.** منو/مجوز/سرویس/مپِ EF/مهاجرت همه از `IModule` می‌آیند؛ هیچ hard-code در هسته.
3. **قابلیتِ حذف (removability).** با غیرفعال/حذفِ ماژول: هسته سالم کار کند، بدونِ منو/گزارش/مجوزِ شکسته و بدونِ فسادِ DB. هسته هرگز به schemaی ماژول `JOIN` نمی‌زند. دادهٔ تاریخی حفظ می‌شود.
4. **DB جداگانه.** هر جدولِ ماژول در **schemaی اختصاصیِ خودش** (مثلِ `Tur`/`Htl`)؛ مهاجرت idempotent و از `GetMigrationScripts()`.
5. **decoupling از طریقِ اینترفیسِ هسته.** اگر هسته به دادهٔ ماژول نیاز دارد (مثلِ تزریقِ پورسانت به حقوق)، هسته **اینترفیس** تعریف می‌کند و ماژول پیاده‌سازی‌اش را register می‌کند — هسته هرگز نوعِ ماژول را نمی‌بیند.
6. **به‌روزرسانیِ مستقل (قاعدهٔ ۸).** تغییرِ ماژول باید **درونِ اسمبلیِ خودِ ماژول** بماند و فقط به‌صورتِ `.mspkg` منتشر شود؛ **نوع‌های مشترکِ هسته را برای یک قابلیتِ ماژول تغییر نده** (کلِ برنامه را مجبور به آپدیت می‌کند). نیازِ واقعیِ هسته را **یک‌بار** پشتِ یک اینترفیسِ پایدار ببر.

---

## ۴) چطور سیستم ماژول را بارگذاری/می‌شناسد

دو مسیر:

**الف) ماژولِ همراهِ نصاب (bundled):** در `App.xaml.cs` (و `API/Program.cs`) داخلِ آرایهٔ `IModule[] bundledModules` یک نمونه از کلاسِ ماژول افزوده می‌شود. سپس برای هر ماژول:
`services.AddSingleton<IModule>(module)` + `module.RegisterServices(services)`؛ و `ApplicationDbContext` فهرستِ `IEnumerable<IModule>` را می‌گیرد و در `OnModelCreating` فقط `ConfigureModel`ِ ماژول‌های **فعال** را صدا می‌زند (`ModuleAwareModelCacheKeyFactory` کشِ مدل را با تغییرِ ماژول‌های فعال بازمی‌سازد).

**ب) ماژولِ دانلودیِ runtime:** `Infrastructure/Modules/ModuleLoader` پوشهٔ `%AppData%/SamaHesab/modules/*.mspkg` را اکسترکت، DLL را با `AssemblyLoadContext` بارگذاری و کلاسِ `IModule` را کشف می‌کند (با dedupe نسبت به bundleها). بدونِ rebuildِ هسته.

**فعال/غیرفعال:** `ModuleService` (در `SamaHesab.WPF/Services`) فهرستِ ماژول‌های اختیاری و وضعیتِ فعال‌بودن را نگه می‌دارد. برای دیده‌شدنِ ماژولِ نو در فهرست، یک `ModuleDef(Key, Name, Core:false, Icon, Version)` به `ModuleService.OptionalModules` افزوده شود (Key باید با `IModule.Key` برابر باشد).

---

## ۵) بسته‌بندی و نسخه‌گذاری

- هر ماژول → `<Name>.dll` + **`module.json`** (`Key`/`Name`/`Version`/`schema`/`dependencies`) + `version.json` + `icon.png` → بستهٔ **`<Name>.mspkg`**.
- ساخت با `installer/build-modules.ps1`؛ کاتالوگِ `modules-catalog.json` تولید و به release `modules` در `kish210/SamaHesab` push می‌شود.
- **نسخه‌گذاری:** `IModule.Version` و `module.json.Version` با هر تغییر bump شوند. نصابِ کاملِ برنامه فقط برای تغییرِ هسته/قراردادِ `IModule` لازم است — نه برای آپدیتِ یک ماژول.

---

## ۶) ماژول‌های نیازمندِ سرور (API)

اگر ماژول از طریقِ مرورگر/موبایل کار می‌کند و به **سرورِ سما حساب (API)** نیاز دارد (مثلِ پنلِ وب):
- آن را در `ModuleShortcuts.ApiDependent` علامت بزن؛ سیستم هنگامِ فعال‌سازی **به کاربر اعلام می‌کند** که سرور باید اجرا باشد و **میانبرِ پیش‌نیازِ «سرورِ سما حساب (API)»** را روی دسکتاپ می‌سازد.
- میانبرِ خودِ ماژول می‌تواند یک `.url` به نشانیِ پنل باشد (مثلِ `http://<server>:5080/seller/`).

---

## ۷) چک‌لیستِ پذیرشِ یک ماژولِ نو

- [ ] پروژهٔ `SamaHesab.Modules.<Name>` + کلاسِ `IModule` با `Key`/`Version`.
- [ ] هیچ ارجاعی از هسته به ماژول نیست (جهتِ وابستگی درست).
- [ ] موجودیت‌ها در schemaی اختصاصی؛ مهاجرتِ idempotent از `GetMigrationScripts()`.
- [ ] منو/مجوز فقط از `IModule`؛ نیازِ دادهٔ هسته فقط از طریقِ اینترفیسِ هسته.
- [ ] `ModuleDef` در `ModuleService.OptionalModules` (Key یکسان، Version).
- [ ] ثبت در آرایهٔ `bundledModules` (App + API) **یا** انتشار به‌صورتِ `.mspkg`.
- [ ] **تستِ removability**: غیرفعال‌کردنِ ماژول هسته را نمی‌شکند.
- [ ] build سبز + کلِ تست‌ها سبز.
- [ ] اگر نیازمندِ API است: علامتِ `ApiDependent` + اعلامِ پیش‌نیاز.
- [ ] تغییرِ ماژول درونِ اسمبلیِ خودش ماند (نوع‌های هسته دست‌نخورده) و `Version` bump شد.
