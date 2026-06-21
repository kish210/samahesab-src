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

/// <summary>فاز ۱۲ (RC-6) — دفترِ روزنامه: فهرستِ زمانیِ آرتیکل‌های اسناد در بازه + خروجیِ اکسل/PDF.</summary>
public partial class DaybookViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;
    private readonly IPdfService _pdf;

    public ObservableCollection<DaybookRow> Rows { get; } = new();

    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private decimal _totalDebit;
    [ObservableProperty] private decimal _totalCredit;
    [ObservableProperty] private DaybookRow? _selectedRow;   // drill-down به سند

    /// <summary>drill-down: بازکردنِ سندِ همان ردیف در یک Tab (دابل‌کلیک/Enter/منو).</summary>
    [RelayCommand]
    private void OpenVoucher()
    {
        if (SelectedRow is { VoucherId: > 0 } r)
            _navigationService.NavigateTo("VoucherEdit", r.VoucherId);
    }

    public DaybookViewModel(IMediator mediator, IPersianCalendarService calendar, IPdfService pdf,
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
            var rows = await _mediator.Send(new GetDaybookQuery(FromDate, ToDate));
            Rows.Clear();
            foreach (var r in rows) Rows.Add(r);
            TotalDebit = rows.Sum(r => r.Debit);
            TotalCredit = rows.Sum(r => r.Credit);
        }, "در حال تهیهٔ دفترِ روزنامه...");
    }

    private ReportTable BuildTable()
    {
        string N(decimal d) => d.ToString("N0");
        var headers = new[] { "تاریخ", "شمارهٔ سند", "کدِ حساب", "نامِ حساب", "شرح", "بدهکار", "بستانکار" };
        var rows = Rows.Select(r => new[] { r.Date, r.VoucherNumber, r.AccountCode, r.AccountName, r.Description, N(r.Debit), N(r.Credit) }).ToList();
        rows.Add(new[] { "", "", "", "", "جمعِ کل", N(TotalDebit), N(TotalCredit) });
        return new ReportTable($"دفترِ روزنامه ({FromDate} تا {ToDate})", headers, rows);
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
        var path = System.IO.Path.Combine(dir, $"دفتر_روزنامه_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}
