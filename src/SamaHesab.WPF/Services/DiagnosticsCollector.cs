using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient;
using SamaHesab.Application.Common.Interfaces;

namespace SamaHesab.WPF.Services;

/// <summary>
/// 🆘 HC-1 — عکسِ فوریِ سلامتِ سیستم برای صفحهٔ «عیب‌یابی» و الصاقِ خودکار در گزارشِ باگ.
/// <para>⚠️ امنیت: این عکس فقط دادهٔ <b>تشخیصی/فنی</b> دارد — هیچ دادهٔ مالی/فاکتور/تراکنشِ مشتری
/// در آن نیست. فقط همین مجموعه مجاز به ارسال به سرورِ پشتیبانی است.</para>
/// </summary>
public sealed record DiagnosticsInfo(
    string ErpVersion,
    string BuildNumber,
    string WindowsVersion,
    string Framework,
    string DatabaseProvider,
    string DatabaseStatus,
    string DatabaseVersion,
    long? ConnectionLatencyMs,
    string LicenseType,
    string LicenseState,
    string MachineFingerprint,
    string User,
    string Branch,
    IReadOnlyList<string> InstalledModules,
    double MemoryUsageMb,
    double DiskFreeGb,
    double DiskTotalGb,
    DateTime CollectedAtUtc)
{
    /// <summary>متنِ خواناِ گزارش (برای کپی/ذخیره/الصاق). بدونِ دادهٔ کسب‌وکار.</summary>
    public string ToReportText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═══ گزارشِ عیب‌یابیِ سما حساب ═══");
        sb.AppendLine($"تاریخِ تهیه (UTC): {CollectedAtUtc:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"نسخهٔ ERP: {ErpVersion}  ·  Build: {BuildNumber}");
        sb.AppendLine($"چارچوب: {Framework}");
        sb.AppendLine($"ویندوز: {WindowsVersion}");
        sb.AppendLine($"پایگاه‌داده: {DatabaseProvider} — {DatabaseStatus}" +
                      (ConnectionLatencyMs is { } ms ? $" ({ms}ms)" : ""));
        sb.AppendLine($"نسخهٔ پایگاه‌داده: {DatabaseVersion}");
        sb.AppendLine($"لایسنس: {LicenseType} ({LicenseState})");
        sb.AppendLine($"اثرانگشتِ ماشین: {MachineFingerprint}");
        sb.AppendLine($"کاربر: {User}  ·  شعبه: {Branch}");
        sb.AppendLine($"حافظهٔ مصرفی: {MemoryUsageMb:N0} MB");
        sb.AppendLine($"دیسک: {DiskFreeGb:N1} از {DiskTotalGb:N1} گیگابایت آزاد");
        sb.AppendLine($"ماژول‌های فعال: {string.Join("، ", InstalledModules)}");
        return sb.ToString();
    }
}

/// <summary>جمع‌کنندهٔ اطلاعاتِ تشخیصیِ محلی (هیچ دادهٔ کسب‌وکاری جمع نمی‌کند).</summary>
public sealed class DiagnosticsCollector
{
    private readonly LicenseService _license;
    private readonly ModuleService _modules;
    private readonly ICurrentUserService _user;

    public DiagnosticsCollector(LicenseService license, ModuleService modules, ICurrentUserService user)
    { _license = license; _modules = modules; _user = user; }

    public async Task<DiagnosticsInfo> CollectAsync(CancellationToken ct = default)
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var ver = asm.GetName().Version?.ToString() ?? "—";
        var build = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?? asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? ver;

        // ── لایسنس ──
        string licType = "—", licState = "—", fingerprint = "—";
        try
        {
            var st = _license.GetStatus();
            licState = st.State.ToString();
            licType = st.License?.Tier.ToString() ?? (st.State == AppLicenseState.Trial ? "آزمایشی" : "—");
            fingerprint = _license.MachineFingerprint;
        }
        catch { /* best-effort */ }

        // ── ماژول‌های فعال ──
        var allModules = _modules.CoreModules.Concat(_modules.OptionalModules);
        var enabled = allModules.Where(m => _modules.IsEnabled(m.Key)).Select(m => m.Name).ToList();

        // ── حافظه/دیسک ──
        double memMb = 0, freeGb = 0, totalGb = 0;
        try { memMb = Process.GetCurrentProcess().WorkingSet64 / 1024d / 1024d; } catch { }
        try
        {
            var root = Path.GetPathRoot(AppSettingsStore.AppDataDir) ?? "C:\\";
            var di = new DriveInfo(root);
            freeGb = di.AvailableFreeSpace / 1024d / 1024d / 1024d;
            totalGb = di.TotalSize / 1024d / 1024d / 1024d;
        }
        catch { }

        // ── پایگاه‌داده (اتصال + نسخه) ──
        var (dbStatus, dbVersion, latency) = await ProbeDatabaseAsync(ct);

        var branch = _user.BranchId is { } b ? $"#{b}" : "—";
        var user = string.IsNullOrWhiteSpace(_user.FullName)
            ? (_user.Username ?? "—")
            : $"{_user.FullName} ({_user.Username})";

        return new DiagnosticsInfo(
            ErpVersion: ver,
            BuildNumber: build,
            WindowsVersion: RuntimeInformation.OSDescription,
            Framework: RuntimeInformation.FrameworkDescription,
            DatabaseProvider: "Microsoft SQL Server",
            DatabaseStatus: dbStatus,
            DatabaseVersion: dbVersion,
            ConnectionLatencyMs: latency,
            LicenseType: licType,
            LicenseState: licState,
            MachineFingerprint: fingerprint,
            User: user,
            Branch: branch,
            InstalledModules: enabled,
            MemoryUsageMb: memMb,
            DiskFreeGb: freeGb,
            DiskTotalGb: totalGb,
            CollectedAtUtc: DateTime.UtcNow);
    }

    private static async Task<(string status, string version, long? latencyMs)> ProbeDatabaseAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(AppSettingsStore.GetConnectionString());
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand("SELECT @@VERSION", conn);
            var raw = (await cmd.ExecuteScalarAsync(ct))?.ToString() ?? "";
            sw.Stop();
            // نخستین خطِ @@VERSION = نام/نسخهٔ موتور.
            var firstLine = raw.Split('\n').FirstOrDefault()?.Trim() ?? raw.Trim();
            return ("متصل ✔", firstLine.Length > 0 ? firstLine : "—", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ("قطع ✖ — " + ex.Message.Split('\n').FirstOrDefault()?.Trim(), "—", null);
        }
    }
}
