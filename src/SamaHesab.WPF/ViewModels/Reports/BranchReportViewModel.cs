using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.BI.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Reports;

/// <summary>F9-7 — گزارشِ تطبیقیِ شعب: فروش/تعدادِ فاکتورِ هر شعبه در بازه + خروجیِ اکسل/PDF.</summary>
public partial class BranchReportViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;
    private readonly IPdfService _pdf;

    public ObservableCollection<BranchPerformanceDto> Rows { get; } = new();

    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private decimal _totalSales;
    [ObservableProperty] private int _totalInvoices;

    public BranchReportViewModel(IMediator mediator, IPersianCalendarService calendar,
        IPdfService pdf, IDialogService dialogService, INavigationService navigationService)
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
            var rows = await _mediator.Send(new GetBranchPerformanceQuery(FromDate, ToDate));
            Rows.Clear();
            foreach (var r in rows) Rows.Add(r);
            TotalSales = Rows.Sum(r => r.Total);
            TotalInvoices = Rows.Sum(r => r.InvoiceCount);
        }, "در حال تهیهٔ گزارشِ شعب...");
    }

    private ReportTable BuildTable()
    {
        string N(decimal d) => d.ToString("N0");
        return new ReportTable($"گزارش تطبیقی شعب ({FromDate} تا {ToDate})",
            new[] { "شعبه", "فروش (ریال)", "تعداد فاکتور", "میانگین فاکتور" },
            Rows.Select(r => new[]
            {
                r.Name, N(r.Total), N(r.InvoiceCount),
                N(r.InvoiceCount > 0 ? r.Total / r.InvoiceCount : 0)
            }).ToList());
    }

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

    private static string SaveTo(string ext, string content)
        => SaveBytes(ext, new System.Text.UTF8Encoding(true).GetBytes(content));

    private static string SaveBytes(string ext, byte[] content)
    {
        var dir = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, $"گزارش_شعب_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}
