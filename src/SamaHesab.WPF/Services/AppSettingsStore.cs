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
}
