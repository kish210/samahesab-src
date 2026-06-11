using System.Globalization;
using MediatR;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Sales.Commands;

namespace SamaHesab.API.Services;

/// <summary>
/// زمان‌بند پس‌زمینه‌ی تولید اسناد/فاکتورهای تکرارشونده (P3، کار #۱۱).
/// هر بازه (پیش‌فرض ۲۴ ساعت) اسناد و فاکتورهای سررسیدشده تا امروز را تولید می‌کند.
/// نکته‌ی چندشرکتی: نسخه‌ی فعلی برای استقرار تک‌شرکتی دقیق است؛ تفکیک per-company بعداً.
/// </summary>
public class RecurringDocumentSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringDocumentSchedulerService> _logger;
    private readonly TimeSpan _interval;

    public RecurringDocumentSchedulerService(IServiceScopeFactory scopeFactory,
        ILogger<RecurringDocumentSchedulerService> logger, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var hours = config.GetValue<double?>("Recurring:IntervalHours") ?? 24;
        _interval = TimeSpan.FromHours(hours <= 0 ? 24 : hours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Recurring scheduler tick failed"); }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var today = TodayPersian();

        var v = await mediator.Send(new GenerateDueRecurringVouchersCommand(today), ct);
        if (v.Succeeded && v.Value is not null)
            _logger.LogInformation("[Recurring] اسناد تکرارشونده تولیدشده: {Count}", v.Value.Generated);

        var i = await mediator.Send(new GenerateDueRecurringInvoicesCommand(today), ct);
        if (i.Succeeded && i.Value is not null)
            _logger.LogInformation("[Recurring] فاکتورهای تکرارشونده تولیدشده: {Count}", i.Value.Generated);
    }

    private static string TodayPersian()
    {
        var pc = new PersianCalendar();
        var n = DateTime.Now;
        return $"{pc.GetYear(n):0000}/{pc.GetMonth(n):00}/{pc.GetDayOfMonth(n):00}";
    }
}
