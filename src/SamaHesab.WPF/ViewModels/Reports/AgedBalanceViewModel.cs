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

/// <summary>فاز ۱۲ (RC) — گزارشِ ماندهٔ سنی‌شدهٔ دریافتنی/پرداختنی (Aged Receivables/Payables) + خروجیِ اکسل/PDF.</summary>
public partial class AgedBalanceViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;
    private readonly IPdfService _pdf;

    public ObservableCollection<AgedRow> Rows { get; } = new();

    [ObservableProperty] private bool _payable;            // false=دریافتنی · true=پرداختنی
    [ObservableProperty] private string _asOfDate = string.Empty;
    [ObservableProperty] private decimal _totalCurrent;
    [ObservableProperty] private decimal _total31_60;
    [ObservableProperty] private decimal _total61_90;
    [ObservableProperty] private decimal _totalOver90;
    [ObservableProperty] private decimal _grandTotal;

    public string Title => Payable ? "ماندهٔ سنی‌شدهٔ پرداختنی (تأمین‌کنندگان)" : "ماندهٔ سنی‌شدهٔ دریافتنی (مشتریان)";

    public AgedBalanceViewModel(IMediator mediator, IPersianCalendarService calendar, IPdfService pdf,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; _pdf = pdf; }

    public override async Task LoadAsync()
    {
        AsOfDate = _calendar.GetCurrentPersianDate();
        await RunAsync();
    }

    partial void OnPayableChanged(bool value) { OnPropertyChanged(nameof(Title)); _ = RunAsync(); }

    [RelayCommand] private void ShowReceivable() => Payable = false;
    [RelayCommand] private void ShowPayable() => Payable = true;

    [RelayCommand]
    private async Task RunAsync()
    {
        await ExecuteAsync(async () =>
        {
            var rows = await _mediator.Send(new GetAgedBalanceQuery(Payable, AsOfDate));
            Rows.Clear();
            foreach (var r in rows) Rows.Add(r);
            TotalCurrent = rows.Sum(r => r.Current);
            Total31_60 = rows.Sum(r => r.D31_60);
            Total61_90 = rows.Sum(r => r.D61_90);
            TotalOver90 = rows.Sum(r => r.Over90);
            GrandTotal = rows.Sum(r => r.Total);
        }, "در حال تهیهٔ ماندهٔ سنی‌شده...");
    }

    private ReportTable BuildTable()
    {
        string N(decimal d) => d.ToString("N0");
        var headers = new[] { Payable ? "تأمین‌کننده" : "مشتری", "جاری (۰–۳۰)", "۳۱–۶۰", "۶۱–۹۰", "بیش از ۹۰", "جمع" };
        var rows = Rows.Select(r => new[] { r.PartyName, N(r.Current), N(r.D31_60), N(r.D61_90), N(r.Over90), N(r.Total) }).ToList();
        rows.Add(new[] { "جمعِ کل", N(TotalCurrent), N(Total31_60), N(Total61_90), N(TotalOver90), N(GrandTotal) });
        return new ReportTable($"{Title} — تا تاریخِ {AsOfDate}", headers, rows);
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
            var meta = new PdfMeta(g.CompanyName, Title, _calendar.GetCurrentPersianDateTime(), Landscape: true);
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
        var path = System.IO.Path.Combine(dir, $"مانده_سنی_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}
