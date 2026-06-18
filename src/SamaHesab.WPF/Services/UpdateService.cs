using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace SamaHesab.WPF.Services;

/// <summary>اطلاعاتِ یک نسخهٔ جدیدِ موجود روی GitHub Releases.</summary>
public record UpdateInfo(Version Version, string Tag, string DownloadUrl, string FileName, string? Notes);

/// <summary>
/// به‌روزرسانِ خودکار از GitHub Releases (پلتفرم/زیرساخت).
/// آخرین Release را می‌خواند، نسخه‌اش را با نسخهٔ اسمبلیِ جاری مقایسه می‌کند،
/// و در صورتِ جدیدتر بودن، نصابِ آن را دانلود و اجرا می‌کند (با تأییدِ کاربر).
/// بررسی فقط در زمانِ استارت‌آپ (سیستم بیکار) انجام می‌شود؛ آفلاین/خطا بی‌صدا رد می‌شود.
/// </summary>
public class UpdateService
{
    private const string Owner = "kish210";
    private const string Repo = "SamaHesab";

    /// <summary>نسخهٔ جاریِ برنامه (از Directory.Build.props روی اسمبلی نشسته).</summary>
    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    /// <summary>اگر نسخهٔ جدیدتری روی GitHub باشد آن را برمی‌گرداند؛ وگرنه null. هرگز استثنا نمی‌دهد.</summary>
    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SamaHesab-Updater");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var json = await http.GetStringAsync(
                $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest", ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!TryParseVersion(tag, out var latest)) return null;
            if (latest <= CurrentVersion) return null;   // چیزی جدیدتر نیست

            // نصابِ دسکتاپ را بردار. ترتیبِ asset‌ها در API تضمینی نیست، پس
            // نصابِ کلاینت/سرور را کنار می‌گذاریم و نصابِ اصلیِ دسکتاپ را ترجیح می‌دهیم.
            if (!root.TryGetProperty("assets", out var assets)) return null;
            var notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;

            (string url, string name)? fallback = null;
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                var url = a.GetProperty("browser_download_url").GetString();
                if (string.IsNullOrEmpty(url)) continue;

                // نصابِ کلاینت/سرور هدفِ آپدیتِ خودکارِ دسکتاپ نیست؛ نادیده بگیر مگر اینکه چیزِ دیگری نباشد.
                bool isClientOrServer = name.IndexOf("Client", StringComparison.OrdinalIgnoreCase) >= 0
                                     || name.IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isClientOrServer) { fallback ??= (url, name); continue; }

                return new UpdateInfo(latest, tag, url, name, notes);
            }
            if (fallback is { } fb) return new UpdateInfo(latest, tag, fb.url, fb.name, notes);
            return null;
        }
        catch { return null; }   // آفلاین/خطا → بی‌صدا
    }

    /// <summary>نصابِ نسخهٔ جدید را در %TEMP% دانلود و اجرا می‌کند. true = اجرا شد (برنامه باید بسته شود).</summary>
    public async Task<bool> DownloadAndRunAsync(UpdateInfo info, CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SamaHesab-Updater");
            var bytes = await http.GetByteArrayAsync(info.DownloadUrl, ct);
            if (bytes.Length < 1_000_000) return false;   // دانلودِ ناقص/خطا (نصاب ده‌ها مگابایت است)

            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), info.FileName);
            await System.IO.File.WriteAllBytesAsync(path, bytes, ct);

            // اجرای نصاب در حالتِ به‌روزرسانیِ درجا: نصاب خودش برنامهٔ در حالِ اجرا را می‌بندد
            // (CLOSEAPPLICATIONS → فایل‌ها قفل نمی‌مانند → نصب کامل می‌شود → میان‌بر سالم می‌ماند)
            // و پس از نصب دوباره اجرا می‌کند (RESTARTAPPLICATIONS). SILENT = بدونِ ویزارد، فقط نوارِ پیشرفت.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
            {
                UseShellExecute = true,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /SUPPRESSMSGBOXES"
            });
            return true;
        }
        catch { return false; }
    }

    private static bool TryParseVersion(string tag, out Version version)
    {
        // «v2.1.0» یا «2.1.0» → Version
        var s = tag.TrimStart('v', 'V').Trim();
        return Version.TryParse(s, out version!);
    }
}
