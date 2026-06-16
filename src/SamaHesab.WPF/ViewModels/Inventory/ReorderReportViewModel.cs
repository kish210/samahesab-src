using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.Application.Reports.Export;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>T17 — گزارشِ «کالاهای زیرِ حداقل/نقطهٔ سفارش» (reorder report): کسری + پیشنهادِ سفارش + خروجی اکسل/PDF.</summary>
public partial class ReorderReportViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPdfService _pdf;

    public ObservableCollection<ReorderReportRow> Rows { get; } = new();

    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private int _itemCount;
    [ObservableProperty] private decimal _totalSuggestedQty;

    public ReorderReportViewModel(IMediator mediator, IPdfService pdf,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _pdf = pdf; }

    public override Task LoadAsync() => RunAsync();

    [RelayCommand]
    private async Task RunAsync()
    {
        await ExecuteAsync(async () =>
        {
            var dto = await _mediator.Send(new GetReorderReportQuery(
                string.IsNullOrWhiteSpace(Search) ? null : Search.Trim()));
            Rows.Clear();
            foreach (var r in dto.Rows) Rows.Add(r);
            ItemCount = dto.ItemCount;
            TotalSuggestedQty = dto.TotalSuggestedQty;
        }, "در حال تهیهٔ گزارشِ نقطهٔ سفارش...");
    }

    private ReportTable BuildTable()
    {
        string N(decimal d) => d.ToString("N0");
        return new ReportTable("گزارش کالاهای زیرِ حداقل / نقطهٔ سفارش",
            new[] { "کد", "نام کالا", "موجودی", "حداقل", "نقطهٔ سفارش", "کسری", "پیشنهادِ سفارش" },
            Rows.Select(r => new[]
            {
                r.Code, r.Name, N(r.OnHand), N(r.MinStock),
                r.ReorderPoint is decimal rp ? N(rp) : "—", N(r.Shortage), N(r.SuggestedQty)
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
            var meta = new PdfMeta(g.CompanyName, null, System.DateTime.Now.ToString("yyyy/MM/dd HH:mm"), Landscape: true);
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
        var path = System.IO.Path.Combine(dir, $"نقطه_سفارش_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}
