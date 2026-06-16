using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Application.Reports.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Reports;

/// <summary>فاز ۱۲ (RC) — خلاصهٔ مالیاتِ ارزش‌افزوده (فروش/خرید) برای اظهارنامه + خروجیِ اکسل/PDF.</summary>
public partial class VatSummaryViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;
    private readonly IPdfService _pdf;

    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private int _salesCount;
    [ObservableProperty] private decimal _salesBase;
    [ObservableProperty] private decimal _outputVat;
    [ObservableProperty] private int _purchaseCount;
    [ObservableProperty] private decimal _purchaseBase;
    [ObservableProperty] private decimal _inputVat;
    [ObservableProperty] private decimal _netPayable;

    public VatSummaryViewModel(IMediator mediator, IPersianCalendarService calendar, IPdfService pdf,
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
            var d = await _mediator.Send(new GetVatSummaryQuery(FromDate, ToDate));
            SalesCount = d.SalesCount; SalesBase = d.SalesBase; OutputVat = d.OutputVat;
            PurchaseCount = d.PurchaseCount; PurchaseBase = d.PurchaseBase; InputVat = d.InputVat;
            NetPayable = d.NetPayable;
        }, "در حال محاسبهٔ مالیاتِ ارزش‌افزوده...");
    }

    private ReportTable BuildTable()
    {
        string N(decimal d) => d.ToString("N0");
        var net = NetPayable >= 0 ? $"{N(NetPayable)} (بدهکار)" : $"{N(-NetPayable)} (بستانکار/استرداد)";
        var rows = new List<string[]>
        {
            new[] { "فروشِ مشمول (پس از تخفیف)", N(SalesBase) },
            new[] { "مالیاتِ فروش — خروجی", N(OutputVat) },
            new[] { "خریدِ مشمول (پس از تخفیف)", N(PurchaseBase) },
            new[] { "مالیاتِ خرید — ورودی", N(InputVat) },
            new[] { "مالیاتِ قابلِ پرداخت (خالص)", net },
        };
        return new ReportTable($"خلاصهٔ مالیاتِ ارزش‌افزوده ({FromDate} تا {ToDate})", new[] { "شرح", "مبلغ (ریال)" }, rows);
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        try { OpenFile(SaveBytes("csv", new System.Text.UTF8Encoding(true).GetBytes(ReportExporter.ToCsv(BuildTable())))); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
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
        try { OpenFile(SaveBytes("html", new System.Text.UTF8Encoding(true).GetBytes(ReportExporter.ToHtml(BuildTable())))); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    private static string SaveBytes(string ext, byte[] content)
    {
        var dir = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, $"خلاصه_مالیات_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}
