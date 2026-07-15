using System.IO;
using System.Text.Json;
using SamaHesab.Application.Licensing;

namespace SamaHesab.WPF.Services;

/// <summary>
/// تعریف یک ماژول. Core=هسته (همیشه فعال، غیرقابل خاموش).
/// Conflicts=کلیدِ ماژول‌هایی که نباید هم‌زمان با این ماژول فعال باشند (تداخلِ متقابل، متقارن اعمال می‌شود).
/// </summary>
public record ModuleDef(string Key, string Name, bool Core, string Icon, string[]? Conflicts = null,
    string Version = "1.0.0");

/// <summary>
/// سیستم فعال‌سازی ماژول‌ها — سماع‌حساب یک «بستر ERP ماژولار» است.
/// هستهٔ حسابداری همیشه فعال؛ ماژول‌های اختیاری (POS/رستوران/گردشگری/…) قابل روشن/خاموش‌اند.
/// وقتی ماژولی خاموش است، منو/صفحه/ناوبریِ آن دیده نمی‌شود و سیستم مثل یک ERP حسابداری حرفه‌ای رفتار می‌کند.
/// </summary>
public class ModuleService
{
    // ── کلیدها ──
    public const string Accounting = "Accounting", Treasury = "Treasury", Sales = "Sales",
        Purchase = "Purchase", Inventory = "Inventory", Customers = "Customers", Reports = "Reports";
    public const string Pos = "POS", Restaurant = "Restaurant", Tourism = "Tourism",
        Hr = "HR", Crm = "CRM", Hotel = "Hotel", Support = "Support", Contracting = "Contracting";
    public const string TaxInvoicing = "TaxInvoicing";
    // کلاینت‌های فروش به‌عنوان ماژولِ مستقل با نسخه‌گذاریِ خودشان (برای ردگیریِ به‌روزرسانی).
    public const string Web = "Web", Mobile = "Mobile";

    /// <summary>ماژول‌های هسته — همیشه فعال، در UI قفل.</summary>
    public IReadOnlyList<ModuleDef> CoreModules { get; } = new[]
    {
        new ModuleDef(Accounting, "حسابداری", true, "FileDocumentOutline"),
        new ModuleDef(Treasury, "خزانه‌داری", true, "Bank"),
        new ModuleDef(Sales, "فروش", true, "CartOutline"),
        new ModuleDef(Purchase, "خرید", true, "TruckOutline"),
        new ModuleDef(Inventory, "انبار", true, "PackageVariant"),
        new ModuleDef(Customers, "اشخاص", true, "AccountGroupOutline"),
        new ModuleDef(Reports, "گزارش‌ها", true, "ChartBar"),
    };

    /// <summary>ماژول‌های اختیاری — قابل خرید/فعال‌سازی.</summary>
    public IReadOnlyList<ModuleDef> OptionalModules { get; } = new[]
    {
        new ModuleDef(Pos, "صندوق فروش (POS)", false, "CreditCardOutline"),
        new ModuleDef(Restaurant, "رستوران", false, "Silverware"),
        // برای ناسازگارکردنِ گردشگری با ماژولی، کلیدش را به Conflicts اضافه کنید، مثلِ:
        //   new ModuleDef(Tourism, "گردشگری", false, "Airplane", Conflicts: new[] { Restaurant, Pos }),
        // فعلاً تداخلی تعریف نشده (انتخابِ کاربر) — مکانیزمِ کنترل آماده است.
        new ModuleDef(Tourism, "گردشگری", false, "Airplane"),
        new ModuleDef(Hr, "منابع انسانی", false, "AccountTie"),
        new ModuleDef(Crm, "باشگاه مشتریان (CRM)", false, "AccountHeartOutline"),
        new ModuleDef(Hotel, "هتل", false, "BedOutline"),
        new ModuleDef(Support, "پشتیبانیِ مشتری", false, "Lifebuoy"),
        // کلاینت‌های فروشِ گردشگری (SELLER-PANEL) — هرکدام نسخهٔ مستقل برای ردگیریِ آپدیت.
        new ModuleDef(Web, "پنلِ وبِ فروش", false, "Web", Version: "1.0.0"),
        new ModuleDef(Mobile, "اپِ موبایلِ فروش", false, "Cellphone", Version: "0.1.0"),
        // پیمانکاری @2026-07-10 دوباره فعال شد (به‌خواستِ کاربر) — با افزودنِ «قراردادهایِ خدماتی»
        // (اشتراکِ اینترنت/پشتیبانی و مانندِ آن) کنارِ backendِ قبلیِ CON-C1 (صورت‌وضعیت/پیمان).
        new ModuleDef(Contracting, "پیمانکاری", false, "HammerWrench"),
        // سامانهٔ مودیان — ارسالِ الکترونیکیِ فاکتورِ فروش به سازمانِ امورِ مالیاتی. بدونِ اعتبارنامهٔ
        // Sandbox/واقعی، «فعال»کردن فقط صف را روشن می‌کند؛ ارسالِ واقعی تا پیکربندیِ گواهی کار نمی‌کند.
        new ModuleDef(TaxInvoicing, "سامانهٔ مودیان", false, "ReceiptTextOutline"),
    };

    private static string FilePath => Path.Combine(AppSettingsStore.AppDataDir, "modules.json");

    /// <summary>مسیرِ فایلِ تنظیماتِ ماژول (برای کپیِ دستی بین ماشین‌ها).</summary>
    public static string ConfigFilePath => FilePath;

    private readonly LicenseService _license;
    private HashSet<string> _enabled;
    public event Action? Changed;

    public ModuleService(LicenseService license)
    {
        _license = license;
        _enabled = Load();
    }

    /// <summary>آیا ماژول فعال است؟ (هسته همیشه true).</summary>
    public bool IsEnabled(string key)
        => CoreModules.Any(m => m.Key == key) || _enabled.Contains(key);

    /// <summary>
    /// U-LIC-MODULE — آیا ماژولِ اختیاریِ <paramref name="key"/> در بستهٔ لایسنسِ فعلی مجاز است؟
    /// هسته و کلاینت‌هایِ فروش (Web/Mobile — هنوز جزوِ قیمت‌گذاریِ per-module نیستند) همیشه مجازند.
    /// </summary>
    public bool IsModuleLicensed(string key)
    {
        if (CoreModules.Any(m => m.Key == key) || key == Web || key == Mobile) return true;
        var tier = _license.GetStatus().License?.Tier ?? LicenseTier.Trial;
        return ModuleEntitlements.IsAllowed(tier, key);
    }

    public void SetEnabled(string key, bool enabled)
    {
        if (CoreModules.Any(m => m.Key == key)) return;   // هسته غیرقابل تغییر
        // کنترلِ تداخل: ماژولی که با یک ماژولِ فعالِ دیگر تداخل دارد، فعال نمی‌شود.
        if (enabled && EnabledConflictName(key) is not null) return;
        if (enabled && !IsModuleLicensed(key)) return;   // U-LIC-MODULE: خارج از بستهٔ لایسنسِ فعلی
        if (enabled) _enabled.Add(key); else _enabled.Remove(key);
        Save();
        Changed?.Invoke();
    }

    /// <summary>تلاش برای فعال/غیرفعال‌سازی با گزارشِ خطای تداخل/لایسنس (برای UI).</summary>
    public bool TrySetEnabled(string key, bool enabled, out string? error)
    {
        error = null;
        if (CoreModules.Any(m => m.Key == key)) return true;   // هسته — بی‌اثر، خطا نده
        if (enabled && EnabledConflictName(key) is string conflict)
        {
            var name = AllModules.FirstOrDefault(m => m.Key == key)?.Name ?? key;
            error = $"«{name}» با «{conflict}» تداخل دارد و هم‌زمان فعال نمی‌شود.";
            return false;
        }
        if (enabled && !IsModuleLicensed(key))
        {
            var name = AllModules.FirstOrDefault(m => m.Key == key)?.Name ?? key;
            error = $"«{name}» در بستهٔ لایسنسِ فعلیِ شما نیست. برایِ فعال‌سازی، بستهٔ لایسنس را ارتقا دهید.";
            return false;
        }
        SetEnabled(key, enabled);
        return true;
    }

    /// <summary>همهٔ ماژول‌ها (هسته + اختیاری).</summary>
    public IEnumerable<ModuleDef> AllModules => CoreModules.Concat(OptionalModules);

    /// <summary>کلیدِ ماژول‌هایی که با <paramref name="key"/> تداخل دارند (متقارن: هر دو سمت).</summary>
    public IReadOnlyCollection<string> ConflictsOf(string key)
    {
        var set = new HashSet<string>();
        var self = AllModules.FirstOrDefault(m => m.Key == key);
        if (self?.Conflicts is { } own) foreach (var c in own) set.Add(c);
        foreach (var m in AllModules)
            if (m.Conflicts is { } cs && cs.Contains(key)) set.Add(m.Key);
        set.Remove(key);
        return set;
    }

    /// <summary>اگر ماژولی که با <paramref name="key"/> تداخل دارد همین حالا فعال است، نامِ آن را برمی‌گرداند؛ وگرنه null.</summary>
    public string? EnabledConflictName(string key)
    {
        foreach (var c in ConflictsOf(key))
            if (IsEnabled(c))
                return AllModules.FirstOrDefault(m => m.Key == c)?.Name ?? c;
        return null;
    }

    /// <summary>کلیدهای ماژول‌های اختیاریِ فعالِ فعلی (برای خروجی/انتقال بین ماشین‌ها).</summary>
    public string[] GetEnabledKeys() => _enabled.OrderBy(k => k).ToArray();

    /// <summary>
    /// جایگزینیِ کاملِ مجموعهٔ ماژول‌های فعال (ورودی از فایلِ ماشینِ دیگر) + ذخیره + اطلاعِ پوسته.
    /// فقط کلیدهای معتبرِ اختیاری پذیرفته می‌شوند (هسته نادیده گرفته می‌شود).
    /// </summary>
    public void ReplaceEnabled(IEnumerable<string> keys)
    {
        var valid = OptionalModules.Select(m => m.Key).ToHashSet();
        // U-SEC-4: پیش‌تر این‌جا کنترلِ تداخل چک نمی‌شد (برخلافِ SetEnabled/TrySetEnabled) — وارد‌کردنِ
        // یک modules.json دست‌کاری‌شده (یا کپی از ماشینِ دیگر با Conflictsِ متفاوت) می‌توانست دو ماژولِ
        // متقابلاً ناسازگار را هم‌زمان فعال کند. فعلاً هیچ Conflicts ای تعریف نشده (بی‌اثر)، ولی مکانیزم
        // باید از همین حالا درست باشد.
        var accepted = new HashSet<string>();
        foreach (var k in keys.Where(valid.Contains).Distinct())
            // U-LIC-MODULE: وارد‌کردنِ modules.json از ماشینِ دیگر (بستهٔ لایسنسِ بالاتر) نباید
            // ماژولی خارج از بستهٔ لایسنسِ این ماشین را فعال کند.
            if (!ConflictsOf(k).Any(accepted.Contains) && IsModuleLicensed(k)) accepted.Add(k);
        _enabled = accepted;
        Save();
        Changed?.Invoke();
    }

    private HashSet<string> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var arr = JsonSerializer.Deserialize<string[]>(File.ReadAllText(FilePath));
                if (arr != null)
                {
                    // هرسِ کلیدهای دیگر-ارائه‌نشده (مثلِ ماژولِ فعلاً غیرفعال‌شده): modules.jsonِ کهنه
                    // نباید ماژولِ خارج‌شده از فهرست را روشن نگه دارد. U-LIC-MODULE: همچنین اگر لایسنس
                    // پس از فعال‌سازیِ قبلی downgrade شده باشد، ماژولِ خارج از بسته دیگر روشن نمی‌ماند.
                    var valid = OptionalModules.Select(m => m.Key).ToHashSet();
                    return new HashSet<string>(arr.Where(valid.Contains).Where(IsModuleLicensed));
                }
            }
        }
        catch { /* fall through to default */ }
        // پیش‌فرضِ یکپارچه (در گیت، منبعِ واحدِ همهٔ ماشین‌ها): چون هر ماشین DB محلیِ خودش را دارد،
        // تنها کانالِ مشترک git است؛ پس مجموعهٔ پیش‌فرض این‌جا تعریف می‌شود تا منوها بین PC/لپ‌تاپ یکسان بماند.
        // گردشگری/هتل خاموش. برای تفاوتِ عمدیِ هر ماشین از «خروجی/ورودیِ تنظیمات» استفاده شود.
        return new HashSet<string>(new[] { Pos, Restaurant, Hr, Crm, Support }.Where(IsModuleLicensed));
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(AppSettingsStore.AppDataDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_enabled.ToArray(),
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best-effort */ }
    }
}
