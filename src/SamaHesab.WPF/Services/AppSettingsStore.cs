using System.IO;
using System.Text.Json;

namespace SamaHesab.WPF.Services;

/// <summary>
/// Stores user-editable settings (connection string) in a writable location
/// under %AppData%\SamaHesab so the app works even when installed to Program Files.
/// </summary>
public static class AppSettingsStore
{
    public static string AppDataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SamaHesab");

    public static string FilePath => Path.Combine(AppDataDir, "settings.user.json");
    public static string LogDirectory => Path.Combine(AppDataDir, "logs");

    // Default points to a local SQL Server Express instance (Windows auth).
    // Change it from the login screen's "تنظیمات اتصال" if your server differs.
    public const string DefaultConnectionString =
        "Server=.\\SQLEXPRESS;Database=SamaHesab;Trusted_Connection=True;" +
        "TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True;Connect Timeout=5;";

    public const string DefaultTheme = "Office2019";

    private class Model
    {
        public Dictionary<string, string> ConnectionStrings { get; set; } = new();
        public string? Theme { get; set; }
        public PrintSettings? Print { get; set; }
        public ApiSettings? Api { get; set; }
        public Dictionary<string, bool>? Modules { get; set; }
        public GeneralSettings? General { get; set; }
    }

    private static Model Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Model>(File.ReadAllText(FilePath)) ?? new Model();
        }
        catch { }
        return new Model();
    }

    private static void Save(Model m)
    {
        Directory.CreateDirectory(AppDataDir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(m, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static string GetTheme()
    {
        var t = Load().Theme;
        return string.IsNullOrWhiteSpace(t) ? DefaultTheme : t!;
    }

    public static void SaveTheme(string theme)
    {
        var m = Load();
        m.Theme = theme;
        if (m.ConnectionStrings.Count == 0)
            m.ConnectionStrings["DefaultConnection"] = DefaultConnectionString;
        Save(m);
    }

    /// <summary>Make sure the directories and a default settings file exist.</summary>
    public static void EnsureInitialized()
    {
        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(LogDirectory);
        if (!File.Exists(FilePath))
            SaveConnectionString(DefaultConnectionString);
    }

    public static string GetConnectionString()
    {
        var m = Load();
        if (m.ConnectionStrings.TryGetValue("DefaultConnection", out var cs) && !string.IsNullOrWhiteSpace(cs))
            return cs;
        return DefaultConnectionString;
    }

    public static void SaveConnectionString(string connectionString)
    {
        var m = Load();
        m.ConnectionStrings["DefaultConnection"] = connectionString;
        Save(m);
    }

    public static PrintSettings GetPrintSettings() => Load().Print ?? new PrintSettings();

    public static void SavePrintSettings(PrintSettings settings)
    {
        var m = Load();
        m.Print = settings;
        Save(m);
    }

    public static ApiSettings GetApiSettings() => Load().Api ?? new ApiSettings();

    public static void SaveApiSettings(ApiSettings settings)
    {
        var m = Load();
        m.Api = settings;
        Save(m);
    }

    /// <summary>وضعیت فعال‌بودن ماژول‌های اختیاری (کلید→روشن/خاموش).</summary>
    public static Dictionary<string, bool> GetModules() => Load().Modules ?? new();

    public static void SaveModules(Dictionary<string, bool> modules)
    {
        var m = Load();
        m.Modules = modules;
        Save(m);
    }

    /// <summary>تنظیماتِ عمومیِ صفحهٔ «تنظیمات» (شرکت/ارز/پیامک).</summary>
    public static GeneralSettings GetGeneral() => Load().General ?? new GeneralSettings();

    public static void SaveGeneral(GeneralSettings g)
    {
        var m = Load();
        m.General = g;
        Save(m);
    }
}

/// <summary>تنظیماتِ عمومیِ کاربر/شرکت که در `settings.user.json` ذخیره می‌شوند.</summary>
public class GeneralSettings
{
    // ── پروفایلِ شرکت (روی سربرگِ فاکتور/گزارش‌ها استفاده می‌شود) ──
    public string? CompanyName { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyNationalId { get; set; }     // شناسهٔ ملی
    public string? CompanyEconomicCode { get; set; }   // کدِ اقتصادی
    public string? CompanyRegNumber { get; set; }      // شمارهٔ ثبت
    public string? CompanyAddress { get; set; }
    public string? CompanyPostalCode { get; set; }
    public string? CompanyEmail { get; set; }
    public string? CompanyWebsite { get; set; }

    public string? FiscalYearStart { get; set; }
    public string? FiscalYearEnd { get; set; }

    // ── مالی ──
    public string Currency { get; set; } = "ریال";
    public decimal DefaultVatRate { get; set; } = 9;   // درصدِ مالیات بر ارزش افزوده
    public int DecimalPlaces { get; set; } = 0;        // ارقامِ اعشار در نمایشِ مبالغ

    // ── شماره‌گذاریِ اسناد ──
    public string VoucherPrefix { get; set; } = "";    // پیشوندِ شمارهٔ سند
    public string InvoicePrefix { get; set; } = "";    // پیشوندِ شمارهٔ فاکتور

    // ── پشتیبان‌گیری ──
    public bool AutoBackupEnabled { get; set; }
    public int BackupIntervalDays { get; set; } = 1;
    public string? BackupPath { get; set; }

    // ── پیامک ──
    public bool SmsEnabled { get; set; }
    public string SmsProvider { get; set; } = "kavenegar";
    public string? SmsApiKey { get; set; }
    public string? SmsSender { get; set; }
}
