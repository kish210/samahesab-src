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
        public bool CompactMode { get; set; }
        public PrintSettings? Print { get; set; }
        public ApiSettings? Api { get; set; }
        public Dictionary<string, bool>? Modules { get; set; }
        public GeneralSettings? General { get; set; }
        public SupportSettings? Support { get; set; }   // 🆘 HC-2
        public PaymentTerminalSettings? PaymentTerminal { get; set; }   // 💳 CR-1
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

    /// <summary>حالتِ فشردهٔ رابط (چگالی). پیش‌فرض: خاموش (عادی).</summary>
    public static bool GetCompactMode() => Load().CompactMode;

    public static void SaveCompactMode(bool compact)
    {
        var m = Load();
        m.CompactMode = compact;
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

    /// <summary>🆘 HC-2 — تنظیماتِ اتصال به سرورِ پشتیبانیِ وردپرس (کلید-API).</summary>
    /// <summary>💳 CR-1 — تنظیماتِ کارت‌خوانِ بانکی.</summary>
    public static PaymentTerminalSettings GetPaymentTerminal() => Load().PaymentTerminal ?? new PaymentTerminalSettings();

    public static void SavePaymentTerminal(PaymentTerminalSettings s)
    {
        var m = Load();
        m.PaymentTerminal = s;
        Save(m);
    }

    public static SupportSettings GetSupport() => Load().Support ?? new SupportSettings();

    public static void SaveSupport(SupportSettings s)
    {
        var m = Load();
        m.Support = s;
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
    public string? BusinessType { get; set; }   // صنف/شغلِ شرکت (از ویزاردِ راه‌اندازی)
    // فعال‌سازیِ سروری (تأییدِ سایت از /register): انقضای محلیِ ذخیره‌شده + ردهٔ لایسنس.
    public string? ServerLicenseExpiryUtc { get; set; }   // ISO UTC؛ خالی = بدونِ فعال‌سازیِ سروری
    public string? ServerLicenseTier { get; set; }

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
    public string? CloudBackupFolder { get; set; }   // پوشهٔ سینکِ ابری (Google Drive for Desktop) — کپیِ خودکارِ بکاپ

    // ── پیامک ──
    public bool SmsEnabled { get; set; }
    public string SmsProvider { get; set; } = "kavenegar";
    public string? SmsApiKey { get; set; }
    public string? SmsSender { get; set; }

    // ── راه‌اندازیِ اولیه (فاز ۱۲ G3) ──
    public bool SetupCompleted { get; set; }   // ویزاردِ First-Run یک‌بار اجرا شده؟
    public int IdleTimeoutMinutes { get; set; }   // قفل/خروجِ خودکار پس از بی‌فعالیتی (۰=خاموش) — امنیتِ تجاری
    public string? CompanyLogoPath { get; set; }
    public string? TrialInstallUtc { get; set; }   // فاز ۱۲ P-G7 — شروعِ دورهٔ آزمایشی (ISO UTC)
    public string? LastBackupUtc { get; set; }      // فاز RC (RC-3) — آخرین پشتیبانِ خودکار (ISO UTC)
    public int PosRoundingStep { get; set; }        // 🇮🇷 POS-IR-1 — پلهٔ گرد کردنِ مبلغِ POS (۰=خاموش، مثلاً ۱۰۰۰)
}

/// <summary>🆘 HC-2 — اتصال به سرورِ پشتیبانیِ وردپرس (kishwifi.com). کلید-API هرگز در ریپو نیست.</summary>
public class SupportSettings
{
    public string BaseUrl { get; set; } = "https://kishwifi.com";   // آدرسِ سایتِ وردپرس
    public string CustomerId { get; set; } = "";                    // شناسهٔ مشتری (از پنلِ پشتیبانی)
    public string ApiKey { get; set; } = "";                        // کلیدِ API
    public string LicenseId { get; set; } = "";                     // شناسهٔ لایسنس
}

/// <summary>💳 CR-1 — تنظیماتِ کارت‌خوانِ بانکی (POS). درایورِ واقعیِ هر PSP بعداً وصل می‌شود.</summary>
public class PaymentTerminalSettings
{
    public bool Enabled { get; set; }                       // کارت‌خوان فعال است؟
    public string Driver { get; set; } = "simulator";       // درایور: simulator | behpardakht | sep | parsian | …
    public string TerminalId { get; set; } = "";            // شناسهٔ پایانه
    public string Port { get; set; } = "";                  // درگاهِ سریال (COMx) یا IP:Port
    public int TimeoutSeconds { get; set; } = 60;
}

