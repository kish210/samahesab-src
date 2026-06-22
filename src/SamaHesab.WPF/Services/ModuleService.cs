using System.IO;
using System.Text.Json;

namespace SamaHesab.WPF.Services;

/// <summary>
/// تعریف یک ماژول. Core=هسته (همیشه فعال، غیرقابل خاموش).
/// Conflicts=کلیدِ ماژول‌هایی که نباید هم‌زمان با این ماژول فعال باشند (تداخلِ متقابل، متقارن اعمال می‌شود).
/// </summary>
public record ModuleDef(string Key, string Name, bool Core, string Icon, string[]? Conflicts = null);

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
        new ModuleDef(Contracting, "پیمانکاری", false, "HammerWrench"),   // CON-C2-5
    };

    private static string FilePath => Path.Combine(AppSettingsStore.AppDataDir, "modules.json");

    /// <summary>مسیرِ فایلِ تنظیماتِ ماژول (برای کپیِ دستی بین ماشین‌ها).</summary>
    public static string ConfigFilePath => FilePath;

    private HashSet<string> _enabled;
    public event Action? Changed;

    public ModuleService() => _enabled = Load();

    /// <summary>آیا ماژول فعال است؟ (هسته همیشه true).</summary>
    public bool IsEnabled(string key)
        => CoreModules.Any(m => m.Key == key) || _enabled.Contains(key);

    public void SetEnabled(string key, bool enabled)
    {
        if (CoreModules.Any(m => m.Key == key)) return;   // هسته غیرقابل تغییر
        // کنترلِ تداخل: ماژولی که با یک ماژولِ فعالِ دیگر تداخل دارد، فعال نمی‌شود.
        if (enabled && EnabledConflictName(key) is not null) return;
        if (enabled) _enabled.Add(key); else _enabled.Remove(key);
        Save();
        Changed?.Invoke();
    }

    /// <summary>تلاش برای فعال/غیرفعال‌سازی با گزارشِ خطای تداخل (برای UI).</summary>
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
        _enabled = new HashSet<string>(keys.Where(valid.Contains));
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
                if (arr != null) return new HashSet<string>(arr);
            }
        }
        catch { /* fall through to default */ }
        // پیش‌فرضِ یکپارچه (در گیت، منبعِ واحدِ همهٔ ماشین‌ها): چون هر ماشین DB محلیِ خودش را دارد،
        // تنها کانالِ مشترک git است؛ پس مجموعهٔ پیش‌فرض این‌جا تعریف می‌شود تا منوها بین PC/لپ‌تاپ یکسان بماند.
        // گردشگری/هتل خاموش. برای تفاوتِ عمدیِ هر ماشین از «خروجی/ورودیِ تنظیمات» استفاده شود.
        return new HashSet<string> { Pos, Restaurant, Hr, Crm, Support };
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
