using System.Diagnostics;
using System.Net.Http;
using System.Windows.Forms;

namespace SamaHesab.ApiTray;

// SamaHesabApiTray.exe — آیکونِ سینی‌سیستم برایِ سرورِ API. پیش‌تر سرور یا با پنجرهٔ کنسولِ لختِ
// خودش اجرا می‌شد یا از طریقِ Registry Run بی‌سروصدا در پس‌زمینه — کاربر هیچ راهی برایِ دیدنِ
// «آیا سرور بالاست؟» یا توقفِ تمیزش نداشت جز Task Manager. این برنامه سرورِ API را به‌عنوانِ
// فرزندِ بی‌پنجره اجرا می‌کند و وضعیتش را در سینیِ ویندوز نشان می‌دهد.
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "Global\\SamaHesabApiTray", out var isNew);
        if (!isNew)
        {
            MessageBox.Show("آیکونِ سرورِ API از قبل در حالِ اجراست.", "سما حساب — سرورِ API",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private const string ApiExeName = "SamaHesab.API.exe";
    private const string ServerUrl = "http://localhost:5080";
    private const string HealthUrl = ServerUrl + "/health";

    private readonly string _serverDir;
    private readonly string _apiExePath;
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _startItem;
    private readonly ToolStripMenuItem _stopItem;
    private readonly System.Windows.Forms.Timer _watchTimer;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };

    private Process? _process;
    private bool _stoppedByUser;
    private bool _healthy;

    public TrayContext()
    {
        _serverDir = AppContext.BaseDirectory;
        _apiExePath = Path.Combine(_serverDir, ApiExeName);

        var menu = new ContextMenuStrip();
        _statusItem = new ToolStripMenuItem("وضعیت: نامشخص") { Enabled = false };
        _startItem = new ToolStripMenuItem("▶ شروعِ سرور", null, (_, _) => StartServer());
        _stopItem = new ToolStripMenuItem("■ توقفِ سرور", null, (_, _) => StopServer(byUser: true));
        var openItem = new ToolStripMenuItem("🌐 بازکردنِ آدرسِ سرور در مرورگر", null, (_, _) => OpenInBrowser());
        var exitItem = new ToolStripMenuItem("خروج", null, (_, _) => ExitTray());
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startItem);
        menu.Items.Add(_stopItem);
        menu.Items.Add(openItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        var iconPath = Path.Combine(_serverDir, "tray.ico");
        _icon = new NotifyIcon
        {
            Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application,
            Text = "سرورِ API سما حساب",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => OpenInBrowser();

        _watchTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _watchTimer.Tick += async (_, _) => await CheckStatusAsync();

        if (!File.Exists(_apiExePath))
        {
            MessageBox.Show(
                $"فایلِ {ApiExeName} کنارِ این برنامه یافت نشد.\nمسیرِ جست‌وجو: {_apiExePath}",
                "سما حساب — سرورِ API", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ExitTray();
            return;
        }

        StartServer();
        _watchTimer.Start();
    }

    private void StartServer()
    {
        if (_process is { HasExited: false }) return;
        try
        {
            _process = Process.Start(new ProcessStartInfo(_apiExePath)
            {
                WorkingDirectory = _serverDir,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            _stoppedByUser = false;
            UpdateStatus("در حال بالاآمدن...", healthy: false);
            _icon.ShowBalloonTip(3000, "سما حساب", "سرورِ API در حال اجراست.", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            UpdateStatus("خطا در اجرا", healthy: false);
            MessageBox.Show($"اجرایِ سرورِ API ناموفق بود:\n{ex.Message}", "سما حساب — سرورِ API",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopServer(bool byUser)
    {
        _stoppedByUser = byUser;
        if (_process is { HasExited: false })
        {
            try { _process.Kill(entireProcessTree: true); } catch { /* در حالِ توقف بود */ }
        }
        UpdateStatus("متوقف شد", healthy: false);
        if (byUser) _icon.ShowBalloonTip(3000, "سما حساب", "سرورِ API متوقف شد.", ToolTipIcon.Info);
    }

    private async Task CheckStatusAsync()
    {
        if (_process is null || _process.HasExited)
        {
            if (!_stoppedByUser && _process is not null)
            {
                // پروسه بدونِ دستورِ کاربر بسته شده (کرش/خطا) — هشدار بده.
                UpdateStatus("متوقفِ غیرمنتظره!", healthy: false);
                _icon.ShowBalloonTip(5000, "سما حساب", "سرورِ API به‌طورِ غیرمنتظره متوقف شد.", ToolTipIcon.Warning);
                _process = null;
            }
            return;
        }

        try
        {
            var resp = await _http.GetAsync(HealthUrl);
            UpdateStatus(resp.IsSuccessStatusCode ? "در حال اجرا ✅" : "پاسخِ نامعتبر از سرور", resp.IsSuccessStatusCode);
        }
        catch
        {
            UpdateStatus("در حال بالاآمدن...", healthy: false);
        }
    }

    private void UpdateStatus(string text, bool healthy)
    {
        _healthy = healthy;
        _statusItem.Text = $"وضعیت: {text}";
        _icon.Text = $"سرورِ API سما حساب — {text}";
        var running = _process is { HasExited: false };
        _startItem.Enabled = !running;
        _stopItem.Enabled = running;
    }

    private static void OpenInBrowser()
    {
        try { Process.Start(new ProcessStartInfo(ServerUrl) { UseShellExecute = true }); }
        catch { /* بی‌اثر — مرورگرِ پیش‌فرض در دسترس نیست */ }
    }

    private void ExitTray()
    {
        _watchTimer.Stop();
        StopServer(byUser: true);
        _icon.Visible = false;
        Application.Exit();
    }
}
