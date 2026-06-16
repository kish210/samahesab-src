using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Application.Reports.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Reports;

/// <summary>فاز ۱۲ (پولیش) — گزارشِ سود و زیانِ کالا/فروش (درآمد − بهای تمام‌شده) + حاشیهٔ سود + خروجی.</summary>
public partial class ProductProfitViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;
    private readonly IPdfService _pdf;

    public ObservableCollection<ProductProfitRow> Rows { get; } = new();

    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private decimal _totalRevenue;
    [ObservableProperty] private decimal _totalCost;
    [ObservableProperty] private decimal _totalProfit;
    [ObservableProperty] private decimal _marginPercent;

    public ProductProfitViewModel(IMediator mediator, IPersianCalendarService calendar, IPdfService pdf,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; _pdf = pdf; }

    public override async Task LoadAsync()
    {
        var now = DateTime.Now;
        FromDate = $"{_calendar.GetPersianYear(now)}/{_calendar.GetPersianMonth(now):D2}/01";
        ToDate = _calendar.GetCurrentPersianDate();
        await RunAsync();
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new GetProductProfitQuery(FromDate, ToDate));
            Rows.Clear();
            foreach (var r in res.Rows) Rows.Add(r);
            TotalRevenue = res.TotalRevenue;
            TotalCost = res.TotalCost;
            TotalProfit = res.TotalProfit;
            MarginPercent = res.MarginPercent;
        }, "در حال محاسبهٔ سود و زیانِ کالا...");
    }

    private ReportTable BuildTable()
    {
        string N(decimal d) => d.ToString("N0");
        var headers = new[] { "کد", "نام کالا", "تعداد", "فروش (ریال)", "بهای تمام‌شده", "سودِ ناخالص", "حاشیه ٪" };
        var rows = Rows.Select(r => new[] { r.Code, r.Name, N(r.Quantity), N(r.Revenue), N(r.Cost), N(r.Profit), r.MarginPercent.ToString("0.#") }).ToList();
        rows.Add(new[] { "", "جمعِ کل", "", N(TotalRevenue), N(TotalCost), N(TotalProfit), MarginPercent.ToString("0.#") });
        return new ReportTable($"سود و زیانِ کالا/فروش ({FromDate} تا {ToDate})", headers, rows);
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (Rows.Count == 0) { await _dialogService.ShowWarningAsync("ابتدا گزارش را تهیه کنید."); return; }
        try { OpenFile(SaveBytes("csv", new System.Text.UTF8Encoding(true).GetBytes(ReportExporter.ToCsv(BuildTable())))); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (Rows.Count == 0) { await _dialogService.ShowWarningAsync("ابتدا گزارش را تهیه کنید."); return; }
        try
        {
            var g = AppSettingsStore.GetGeneral();
            var meta = new PdfMeta(g.CompanyName, $"بازه: {FromDate} تا {ToDate}", _calendar.GetCurrentPersianDateTime(), Landscape: true);
            OpenFile(SaveBytes("pdf", _pdf.RenderTable(BuildTable(), meta)));
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (Rows.Count == 0) { await _dialogService.ShowWarningAsync("ابتدا گزارش را تهیه کنید."); return; }
        try { OpenFile(SaveBytes("html", new System.Text.UTF8Encoding(true).GetBytes(ReportExporter.ToHtml(BuildTable())))); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    private static string SaveBytes(string ext, byte[] content)
    {
        var dir = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, $"سود_کالا_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}
