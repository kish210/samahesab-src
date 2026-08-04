using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace SamaHesab.WPF.Services;

/// <summary>اطلاعاتِ یک نسخهٔ جدیدِ موجود روی سرورِ دانلود.
/// <c>IsWebOnly</c> — از v2.9 به بعد تنها نصابِ منتشرشده «Web_Setup» (سرور+وب) است، نه نصابِ
/// دسکتاپ؛ اجرایِ خودکارِ آن رویِ نصبِ دسکتاپِ کاربر (CLOSEAPPLICATIONS/RESTARTAPPLICATIONS)
/// نادرست است — کاربرِ دسکتاپ باید آگاهانه به کلاینتِ وب مهاجرت کند، نه با یک دانلودِ خودکار.</summary>
public record UpdateInfo(Version Version, string Tag, string DownloadUrl, string FileName, string? Notes, bool IsWebOnly = false);

/// <summary>
/// به‌روزرسانِ خودکار — منبعِ نسخه/فایل از `https://kishwifi.com/download/version.json`
/// (سرورِ پشتیبانیِ وردپرسِ کاربر، آپلودِ دستی از installer/Output از طریقِ cPanel؛ نگاه کن به
/// پوشهٔ محلیِ `download/` که نمونهٔ همین فایل‌ها را برایِ آپلود آماده می‌کند).
/// منبعِ قبلی GitHub Releasesِ عمومی بود؛ به‌درخواستِ کاربر (@2026-07-22) این منبع اضافه/جایگزین شد
/// چون کاربر می‌خواست نصاب‌ها را روی دامنهٔ خودش هم میزبانی کند، نه فقط GitHub.
/// قالبِ version.json: {"version":"X.Y.Z","notes":"...","files":[{"name":"...","url":"..."}]}
/// آخرین نسخه را می‌خواند، با نسخهٔ اسمبلیِ جاری مقایسه می‌کند، و در صورتِ جدیدتر بودن،
/// نصابِ آن را دانلود و اجرا می‌کند (با تأییدِ کاربر). بررسی فقط در زمانِ استارت‌آپ انجام
/// می‌شود؛ آفلاین/خطا بی‌صدا رد می‌شود.
/// </summary>
public class UpdateService
{
    private const string ManifestUrl = "https://kishwifi.com/download/version.json";

    /// <summary>صفحهٔ دانلودِ عمومی برایِ راهنماییِ کاربرِ دسکتاپ به نصابِ وبِ جدید.</summary>
    public const string WebDownloadPageUrl = "https://kishwifi.com/download/";

    /// <summary>نسخهٔ جاریِ برنامه (از Directory.Build.props روی اسمبلی نشسته).</summary>
    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    /// <summary>اگر نسخهٔ جدیدتری روی سرورِ دانلود باشد آن را برمی‌گرداند؛ وگرنه null. هرگز استثنا نمی‌دهد.</summary>
    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SamaHesab-Updater");

            var json = await http.GetStringAsync(ManifestUrl, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.GetProperty("version").GetString() ?? "";
            if (!TryParseVersion(tag, out var latest)) return null;
            if (latest <= CurrentVersion) return null;   // چیزی جدیدتر نیست

            // نصابِ دسکتاپ را بردار. ترتیبِ فایل‌ها در manifest تضمینی نیست، پس
            // نصابِ کلاینت/سرور را کنار می‌گذاریم و نصابِ اصلیِ دسکتاپ را ترجیح می‌دهیم.
            if (!root.TryGetProperty("files", out var files)) return null;
            var notes = root.TryGetProperty("notes", out var b) ? b.GetString() : null;

            (string url, string name, bool isWebOnly)? fallback = null;
            foreach (var f in files.EnumerateArray())
            {
                var name = f.GetProperty("name").GetString() ?? "";
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                var url = f.GetProperty("url").GetString();
                if (string.IsNullOrEmpty(url)) continue;

                // نصابِ کلاینت/سرور/وب هدفِ آپدیتِ خودکارِ دسکتاپ نیست؛ نادیده بگیر مگر اینکه چیزِ دیگری نباشد.
                bool isWebOnly = name.IndexOf("Web_Setup", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isClientOrServer = name.IndexOf("Client", StringComparison.OrdinalIgnoreCase) >= 0
                                     || name.IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isWebOnly) { fallback ??= (url, name, true); continue; }
                if (isClientOrServer) { fallback ??= (url, name, false); continue; }

                return new UpdateInfo(latest, tag, url, name, notes);
            }
            // از v2.9 به بعد معمولاً فقط Web_Setup منتشر می‌شود ⇒ به همان fallback می‌رسیم —
            // IsWebOnly=true یعنی «پیشنهادِ آگاهانهٔ مهاجرت»، نه دانلود/اجرایِ خودکار.
            if (fallback is { } fb) return new UpdateInfo(latest, tag, fb.url, fb.name, notes, fb.isWebOnly);
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
