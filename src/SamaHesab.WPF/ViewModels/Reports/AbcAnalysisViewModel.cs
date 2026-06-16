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

/// <summary>فاز ۱۲ (پولیش) — تحلیلِ ABCِ کالا بر اساسِ ارزشِ فروش (A/B/C) + خروجی.</summary>
public partial class AbcAnalysisViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;
    private readonly IPdfService _pdf;

    public ObservableCollection<AbcRow> Rows { get; } = new();

    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private int _countA;
    [ObservableProperty] private int _countB;
    [ObservableProperty] private int _countC;
    [ObservableProperty] private decimal _totalValue;

    public AbcAnalysisViewModel(IMediator mediator, IPersianCalendarService calendar, IPdfService pdf,
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
            var res = await _mediator.Send(new GetAbcAnalysisQuery(FromDate, ToDate));
            Rows.Clear();
            foreach (var r in res.Rows) Rows.Add(r);
            CountA = res.CountA; CountB = res.CountB; CountC = res.CountC;
            TotalValue = res.TotalValue;
        }, "در حال تحلیلِ ABC...");
    }

    private ReportTable BuildTable()
    {
        string N(decimal d) => d.ToString("N0");
        var headers = new[] { "طبقه", "کد", "نام کالا", "ارزشِ فروش", "سهم ٪", "تجمعی ٪" };
        var rows = Rows.Select(r => new[] { r.Class, r.Code, r.Name, N(r.Value), r.SharePercent.ToString("0.##"), r.CumulativePercent.ToString("0.##") }).ToList();
        return new ReportTable($"تحلیلِ ABCِ کالا ({FromDate} تا {ToDate}) — A:{CountA} B:{CountB} C:{CountC}", headers, rows);
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
            var meta = new PdfMeta(g.CompanyName, $"بازه: {FromDate} تا {ToDate}", _calendar.GetCurrentPersianDateTime());
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
        var path = System.IO.Path.Combine(dir, $"تحلیل_ABC_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}
