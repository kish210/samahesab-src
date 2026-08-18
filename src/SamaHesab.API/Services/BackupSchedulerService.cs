using SamaHesab.Application.Common.Interfaces;

namespace SamaHesab.API.Services;

/// <summary>
/// زمان‌بند پس‌زمینه‌ی پشتیبان‌گیری خودکارِ سرور (RC-3 — بحرانیِ #۳ گزارشِ آمادگیِ تجاری).
/// به‌صورت دوره‌ای از کلِ پایگاه‌داده فایلِ .bak می‌گیرد و پشتیبان‌های قدیمی را پاک‌سازی می‌کند
/// (نگه‌داریِ ۳۰ نسخه‌ی آخر — داخلِ AutoBackupAsync).
/// بازه از پیکربندی: Backup:IntervalHours (پیش‌فرض ۲۴)؛ با Backup:Enabled=false خاموش می‌شود.
/// مستقل از HTTP/ICurrentUserService — scope مخصوص خود را می‌سازد (هم‌الگو با سایر زمان‌بندها).
/// </summary>
public class BackupSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupSchedulerService> _logger;
    private readonly TimeSpan _interval;
    private readonly bool _enabled;

    public BackupSchedulerService(IServiceScopeFactory scopeFactory,
        ILogger<BackupSchedulerService> logger, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var hours = config.GetValue<double?>("Backup:IntervalHours") ?? 24;
        _interval = TimeSpan.FromHours(hours <= 0 ? 24 : hours);
        _enabled = config.GetValue<bool?>("Backup:Enabled") ?? true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("پشتیبان‌گیری خودکار غیرفعال است (Backup:Enabled=false).");
            return;
        }

        // کمی تأخیر اولیه تا اپ کامل بالا بیاید و پایگاه‌داده آماده باشد
        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); } catch { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Backup scheduler tick failed"); }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var backup = scope.ServiceProvider.GetRequiredService<IBackupService>();
        var file = await backup.AutoBackupAsync(ct);
        if (file is not null)
            _logger.LogInformation("[Backup] پشتیبان‌گیری خودکار انجام شد: {File}", file);
    }
}
