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

/// <summary>گزارش موجودی/ارزش‌گذاری انبار (هستهٔ ERP): همهٔ انبارها یا یک انبار + خروجی اکسل/PDF.</summary>
public partial class InventoryReportViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPdfService _pdf;

    public ObservableCollection<InvWarehousePick> Warehouses { get; } = new();
    public ObservableCollection<StockRow> Rows { get; } = new();

    [ObservableProperty] private int? _selectedWarehouseId;   // null = همهٔ انبارها
    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private decimal _totalValue;
    [ObservableProperty] private int _itemCount;

    public InventoryReportViewModel(IMediator mediator, IPdfService pdf,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _pdf = pdf; }

    public override async Task LoadAsync()
    {
        Warehouses.Clear();
        Warehouses.Add(new InvWarehousePick(null, "همهٔ انبارها"));
        foreach (var w in await _mediator.Send(new GetWarehousesQuery()))
            Warehouses.Add(new InvWarehousePick(w.Id, w.Name));
        SelectedWarehouseId = null;
        await RunAsync();
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        await ExecuteAsync(async () =>
        {
            var dto = await _mediator.Send(new GetInventoryValuationQuery(SelectedWarehouseId,
                string.IsNullOrWhiteSpace(Search) ? null : Search.Trim()));
            Rows.Clear();
            foreach (var r in dto.Rows) Rows.Add(r);
            TotalValue = dto.TotalValue;
            ItemCount = dto.ItemCount;
        }, "در حال تهیهٔ گزارش انبار...");
    }

    private ReportTable BuildTable()
    {
        string N(decimal d) => d.ToString("N0");
        return new ReportTable("گزارش موجودی و ارزش انبار",
            new[] { "کد", "نام کالا", "موجودی", "بهای میانگین", "ارزش" },
            Rows.Select(r => new[] { r.Code, r.Name, N(r.Quantity), N(r.AverageCost), N(r.Value) }).ToList());
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
            var meta = new PdfMeta(g.CompanyName, null, System.DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
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
        var path = System.IO.Path.Combine(dir, $"موجودی_انبار_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}

public record InvWarehousePick(int? Id, string Display);
