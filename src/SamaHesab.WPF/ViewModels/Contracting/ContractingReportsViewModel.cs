using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Contracting.Queries;
using SamaHesab.Application.Reports.Export;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Contracting;

/// <summary>یک گزینهٔ گزارش در دراپ‌داون.</summary>
public record ContractingReportPick(int Index, string Display);

/// <summary>
/// CON-C2-6 — صفحهٔ گزارش‌های پیمانکاری. سه گزارشِ <see cref="ContractingReportBuilder"/> را از
/// <see cref="GetContractingReportsQuery"/> می‌گیرد، با ستون‌های پویا نشان می‌دهد و
/// به CSV/HTML/PDF خروجی می‌گیرد. هم‌الگوی <c>TourismReportsViewModel</c>.
/// </summary>
public partial class ContractingReportsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPdfService _pdf;
    private ReportTable[] _tables = System.Array.Empty<ReportTable>();

    public ObservableCollection<ContractingReportPick> Reports { get; } =
    [
        new(0, "خلاصهٔ مالیِ پیمان‌ها"),
        new(1, "سپرده‌های نگه‌داشته"),
        new(2, "دفترِ ضمانت‌نامه‌ها"),
    ];

    [ObservableProperty] private int _selectedReportIndex;
    [ObservableProperty] private DataView? _currentView;
    [ObservableProperty] private string _currentTitle = string.Empty;
    [ObservableProperty] private int _rowCount;

    public ContractingReportsViewModel(IMediator mediator, IPdfService pdf,
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
            var dto = await _mediator.Send(new GetContractingReportsQuery());
            _tables = [dto.FinancialSummary, dto.DepositsHeld, dto.Guarantees];
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
        for (int i = 0; i < t.Headers.Count; i++) dt.Columns.Add(t.Headers[i], typeof(string));
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
        var path = System.IO.Path.Combine(dir, $"گزارش_پیمانکاری_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}
