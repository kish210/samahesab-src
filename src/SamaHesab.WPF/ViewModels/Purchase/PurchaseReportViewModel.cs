using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.BI.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Purchase;

/// <summary>
/// کارِ ۸ (هستهٔ ERP) — صفحهٔ گزارشِ خرید: تأمین‌کنندگانِ برتر / روندِ ماهانهٔ خرید
/// روی کوئری‌های تحلیلِ موجود (`GetTopSuppliers`/`GetPurchaseTrend`) + بازهٔ تاریخ + خلاصهٔ جمعِ خرید + خروجی اکسل/PDF.
/// قرینهٔ SalesReport.
/// </summary>
public partial class PurchaseReportViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    public List<string> ReportTypes { get; } = new() { "تأمین‌کنندگانِ برتر", "روندِ خریدِ ماهانه" };

    [ObservableProperty] private string _reportType = "تأمین‌کنندگانِ برتر";
    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;

    [ObservableProperty] private string[] _headers = System.Array.Empty<string>();
    public ObservableCollection<string[]> Rows { get; } = new();

    [ObservableProperty] private decimal _totalPurchases;
    [ObservableProperty] private int _invoiceCount;

    public event System.Action? HeadersChanged;

    public PurchaseReportViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        var today = _calendar.GetCurrentPersianDate();
        ToDate = today;
        FromDate = today.Length >= 7 ? today[..5] + "01/01" : today;
        await RunAsync();
    }

    partial void OnReportTypeChanged(string value) => _ = RunAsync();

    [RelayCommand]
    private async Task RunAsync()
    {
        await ExecuteAsync(async () =>
        {
            Rows.Clear();
            decimal total = 0; int count = 0;
            if (ReportType == "روندِ خریدِ ماهانه")
            {
                Headers = new[] { "دوره (ماه)", "مبلغِ خرید", "تعدادِ فاکتور" };
                foreach (var t in await _mediator.Send(new GetPurchaseTrendQuery(FromDate, ToDate)))
                { Rows.Add(new[] { t.Period, N(t.Total), t.Count.ToString() }); total += t.Total; count += t.Count; }
            }
            else
            {
                Headers = new[] { "تأمین‌کننده", "مبلغِ خرید", "تعدادِ فاکتور" };
                foreach (var s in await _mediator.Send(new GetTopSuppliersQuery(FromDate, ToDate, 50)))
                { Rows.Add(new[] { s.Name, N(s.Total), s.InvoiceCount.ToString() }); total += s.Total; count += s.InvoiceCount; }
            }
            TotalPurchases = total; InvoiceCount = count;
            HeadersChanged?.Invoke();
        }, "در حال تهیهٔ گزارشِ خرید...");
    }

    private static string N(decimal d) => d.ToString("N0");

    private ReportTable BuildTable()
        => new($"گزارش خرید — {ReportType} ({FromDate} تا {ToDate})", Headers, Rows.ToList());

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (Rows.Count == 0) { await _dialogService.ShowWarningAsync("ابتدا گزارش را تهیه کنید."); return; }
        try { OpenFile(SaveTo("csv", ReportExporter.ToCsv(BuildTable()))); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (Rows.Count == 0) { await _dialogService.ShowWarningAsync("ابتدا گزارش را تهیه کنید."); return; }
        try { OpenFile(SaveTo("html", ReportExporter.ToHtml(BuildTable()))); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    private static string SaveTo(string ext, string content)
    {
        var dir = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, $"گزارش_خرید_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllText(path, content, new System.Text.UTF8Encoding(true));
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}
