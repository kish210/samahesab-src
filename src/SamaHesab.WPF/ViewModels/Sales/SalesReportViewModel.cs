using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.BI.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Sales;

/// <summary>
/// کارِ ۸ (هستهٔ ERP) — صفحهٔ گزارشِ فروش: سه گزارشِ تجاری روی کوئری‌های تحلیلِ موجود
/// (مشتریانِ برتر / کالاهای پرفروش / روندِ ماهانه) + بازهٔ تاریخ + خلاصهٔ فروش/سود/حاشیه + خروجی اکسل/PDF.
/// مشابهِ الگوی InventoryReport/FinancialReports با ReportExporter — صفحهٔ جدید نیست؛ تکمیلِ شکافِ گزارشِ هسته.
/// </summary>
public partial class SalesReportViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    public List<string> ReportTypes { get; } = new()
        { "مشتریانِ برتر", "کالاهای پرفروش", "روندِ فروشِ ماهانه" };

    [ObservableProperty] private string _reportType = "مشتریانِ برتر";
    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;

    /// <summary>سرستون‌های گریدِ گزارشِ جاری (بسته به نوعِ گزارش تغییر می‌کند) — نمای آن را code-behind می‌سازد.</summary>
    [ObservableProperty] private string[] _headers = System.Array.Empty<string>();
    public ObservableCollection<string[]> Rows { get; } = new();

    // خلاصهٔ همیشگی (از تحلیلِ سود)
    [ObservableProperty] private decimal _totalSales;
    [ObservableProperty] private decimal _totalProfit;
    [ObservableProperty] private decimal _marginPercent;

    /// <summary>پس از هر اجرا یا تغییرِ نوع، نما باید ستون‌ها را بازبسازد.</summary>
    public event System.Action? HeadersChanged;

    public SalesReportViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        var today = _calendar.GetCurrentPersianDate();          // yyyy/MM/dd
        ToDate = today;
        FromDate = today.Length >= 7 ? today[..5] + "01/01" : today;   // ابتدای سالِ جاری
        await RunAsync();
    }

    partial void OnReportTypeChanged(string value) => _ = RunAsync();

    [RelayCommand]
    private async Task RunAsync()
    {
        await ExecuteAsync(async () =>
        {
            // خلاصهٔ فروش/سود/حاشیه (همیشه)
            var profit = await _mediator.Send(new GetProfitAnalysisQuery(FromDate, ToDate));
            TotalSales = profit.Sales; TotalProfit = profit.Profit; MarginPercent = profit.MarginPercent;

            Rows.Clear();
            switch (ReportType)
            {
                case "کالاهای پرفروش":
                    Headers = new[] { "کالا", "مبلغِ فروش", "تعداد ردیف", "سود" };
                    foreach (var p in profit.TopProducts)
                        Rows.Add(new[] { p.Name, N(p.Total), p.LineCount.ToString(), N(p.Profit) });
                    break;

                case "روندِ فروشِ ماهانه":
                    Headers = new[] { "دوره (ماه)", "مبلغِ فروش", "تعدادِ فاکتور" };
                    foreach (var t in await _mediator.Send(new GetSalesTrendQuery(FromDate, ToDate)))
                        Rows.Add(new[] { t.Period, N(t.Total), t.Count.ToString() });
                    break;

                default: // مشتریانِ برتر
                    Headers = new[] { "مشتری", "مبلغِ فروش", "تعدادِ فاکتور" };
                    foreach (var c in await _mediator.Send(new GetTopCustomersQuery(FromDate, ToDate, 50)))
                        Rows.Add(new[] { c.Name, N(c.Total), c.InvoiceCount.ToString() });
                    break;
            }
            HeadersChanged?.Invoke();
        }, "در حال تهیهٔ گزارشِ فروش...");
    }

    private static string N(decimal d) => d.ToString("N0");

    private ReportTable BuildTable()
        => new($"گزارش فروش — {ReportType} ({FromDate} تا {ToDate})", Headers, Rows.ToList());

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
        var path = System.IO.Path.Combine(dir, $"گزارش_فروش_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllText(path, content, new System.Text.UTF8Encoding(true));
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}
