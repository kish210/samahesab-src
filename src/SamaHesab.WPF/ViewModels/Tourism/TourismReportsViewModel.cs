using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Modules.Tourism.Application.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Tourism;

/// <summary>یک گزینهٔ گزارش در دراپ‌داون.</summary>
public record TourismReportPick(int Index, string Display);

/// <summary>
/// TUR-C2-5 — صفحهٔ گزارش‌های گردشگری. چهار گزارشِ <see cref="TourismReportBuilder"/> را از
/// <see cref="GetTourismReportsQuery"/> می‌گیرد، با ستون‌های پویا در گرید نشان می‌دهد و
/// به CSV/HTML/PDF خروجی می‌گیرد. فیلترِ دوره (ماهِ شمسی YYYYMM) فقط بر پورسانت/عملکرد اثر دارد.
/// </summary>
public partial class TourismReportsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPdfService _pdf;
    private ReportTable[] _tables = System.Array.Empty<ReportTable>();

    public ObservableCollection<TourismReportPick> Reports { get; } =
    [
        new(0, "ماندهٔ ودیعهٔ تأمین‌کنندگان"),
        new(1, "سودِ محصولات"),
        new(2, "پورسانتِ ماهانهٔ فروشندگان"),
        new(3, "عملکردِ فروشندگان"),
    ];

    [ObservableProperty] private int _selectedReportIndex;
    [ObservableProperty] private string _period = string.Empty;   // YYYYMM شمسی؛ خالی = همهٔ دوره‌ها
    [ObservableProperty] private DataView? _currentView;
    [ObservableProperty] private string _currentTitle = string.Empty;
    [ObservableProperty] private int _rowCount;

    public TourismReportsViewModel(IMediator mediator, IPdfService pdf,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _pdf = pdf; }

    public override Task LoadAsync() => RunAsync();

    partial void OnSelectedReportIndexChanged(int value) => ShowSelected();

    [RelayCommand]
    private async Task RunAsync()
    {
        await ExecuteAsync(async () =>
        {
            var period = string.IsNullOrWhiteSpace(Period) ? null : Period.Trim();
            var dto = await _mediator.Send(new GetTourismReportsQuery(period));
            _tables = [dto.SupplierDeposits, dto.ProductProfit, dto.MonthlyCommission, dto.SellerPerformance];
            ShowSelected();
        }, "در حال تهیهٔ گزارش...");
    }

    private void ShowSelected()
    {
        if (_tables.Length == 0) { CurrentView = null; CurrentTitle = string.Empty; RowCount = 0; return; }
        var t = _tables[System.Math.Clamp(SelectedReportIndex, 0, _tables.Length - 1)];
        CurrentView = ToDataTable(t).DefaultView;
        CurrentTitle = t.Title;
        RowCount = t.Rows.Count;
    }

    private static DataTable ToDataTable(ReportTable t)
    {
        var dt = new DataTable();
        foreach (var h in t.Headers) dt.Columns.Add(h, typeof(string));
        // ارقامِ فارسی + جداکنندهٔ هزارگانِ فارسی برای نمایش (ux-prompt §0/§4)؛ خروجیِ CSV/PDF لاتین می‌ماند.
        foreach (var r in t.Rows)
            dt.Rows.Add(r.Select(c => (object)Converters.NumberFormatConverter.ToPersian(c)).ToArray());
        return dt;
    }

    private ReportTable? Selected
        => _tables.Length == 0 ? null : _tables[System.Math.Clamp(SelectedReportIndex, 0, _tables.Length - 1)];

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (Selected is null) { await _dialogService.ShowWarningAsync("ابتدا گزارش را تهیه کنید."); return; }
        try { OpenFile(SaveBytes("csv", new System.Text.UTF8Encoding(true).GetBytes(ReportExporter.ToCsv(Selected)))); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (Selected is null) { await _dialogService.ShowWarningAsync("ابتدا گزارش را تهیه کنید."); return; }
        try { OpenFile(SaveBytes("html", new System.Text.UTF8Encoding(true).GetBytes(ReportExporter.ToHtml(Selected)))); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (Selected is null) { await _dialogService.ShowWarningAsync("ابتدا گزارش را تهیه کنید."); return; }
        try
        {
            var g = AppSettingsStore.GetGeneral();
            var meta = new PdfMeta(g.CompanyName, null, System.DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
            OpenFile(SaveBytes("pdf", _pdf.RenderTable(Selected, meta)));
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    private static string SaveBytes(string ext, byte[] content)
    {
        var dir = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, $"گزارش_گردشگری_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}
