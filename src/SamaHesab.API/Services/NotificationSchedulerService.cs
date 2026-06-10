using System.Globalization;
using SamaHesab.Application.Automation;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.API.Services;

/// <summary>
/// زمان‌بند پس‌زمینه‌ی اعلان‌ها (P2، کار #۷): به‌صورت دوره‌ای برای هر شرکت فعال،
/// اعلان‌های عملیاتی (سررسید چک + کسری موجودی) را محاسبه و ثبت لاگ می‌کند.
/// مستقل از HTTP/ICurrentUserService — scope مخصوص خود را می‌سازد.
/// بازه از پیکربندی: Notifications:IntervalHours (پیش‌فرض ۶).
/// </summary>
public class NotificationSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationSchedulerService> _logger;
    private readonly TimeSpan _interval;

    public NotificationSchedulerService(IServiceScopeFactory scopeFactory,
        ILogger<NotificationSchedulerService> logger, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var hours = config.GetValue<double?>("Notifications:IntervalHours") ?? 6;
        _interval = TimeSpan.FromHours(hours <= 0 ? 6 : hours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // کمی تأخیر اولیه تا اپ کامل بالا بیاید
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); } catch { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Notification scheduler tick failed"); }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var companies = sp.GetRequiredService<IRepository<Company>>();
        var cheques = sp.GetRequiredService<IChequeRepository>();
        var products = sp.GetRequiredService<IRepository<Product>>();
        var stock = sp.GetRequiredService<IRepository<StockItem>>();

        var today = TodayPersian();
        var active = await companies.FindAsync(c => c.IsActive, ct);

        foreach (var company in active)
        {
            var chequeInputs = (await cheques.GetByStatusAsync(company.Id, ChequeStatus.InProcess, ct))
                .Select(c => new ChequeAlertInput(c.Id, c.ChequeNumber, c.DueDate, c.Amount, c.ChequeType.ToString()));

            var withThreshold = await products.FindAsync(
                p => p.CompanyId == company.Id && (p.MinStock > 0 || p.ReorderPoint > 0), ct);
            var ids = withThreshold.Select(p => p.Id).ToList();
            var onHand = (await stock.FindAsync(s => ids.Contains(s.ProductId), ct))
                .GroupBy(s => s.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            var stockInputs = withThreshold.Select(p => new StockAlertInput(
                p.Id, p.Name, onHand.TryGetValue(p.Id, out var q) ? q : 0, p.MinStock, p.ReorderPoint));

            var alerts = AlertEngine.Build(chequeInputs, today, stockInputs);
            if (alerts.Count == 0) continue;

            var critical = alerts.Count(a => a.Severity == AlertSeverity.Critical);
            var warning = alerts.Count(a => a.Severity == AlertSeverity.Warning);
            _logger.LogInformation(
                "[Notifications] شرکت {Company} ({Id}): {Critical} بحرانی، {Warning} هشدار — {Total} اعلان",
                company.Name, company.Id, critical, warning, alerts.Count);
        }
    }

    private static string TodayPersian()
    {
        var pc = new PersianCalendar();
        var n = DateTime.Now;
        return $"{pc.GetYear(n):0000}/{pc.GetMonth(n):00}/{pc.GetDayOfMonth(n):00}";
    }
}
