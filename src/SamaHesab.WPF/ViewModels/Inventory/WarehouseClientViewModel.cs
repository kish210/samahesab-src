using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>
/// کلاینت لمسی انبار (مشابه POS/رستوران، از طریق Web API): رسید ورود، حواله خروج، تعدیل.
/// انتخاب انبار، جست‌وجوی کالا/بارکد، سبد اقلام، ثبت سند و مشاهده‌ی موجودی فعلی.
/// </summary>
public partial class WarehouseClientViewModel : BaseViewModel
{
    private readonly ApiClient _api;

    public ObservableCollection<ApiWarehouse> Warehouses { get; } = new();
    public ObservableCollection<WhProductTile> SearchResults { get; } = new();
    public ObservableCollection<WhCartLine> Cart { get; } = new();
    public ObservableCollection<ApiStockRow> Stock { get; } = new();
    public List<string> Operations { get; } = new() { "رسید ورود", "حواله خروج", "تعدیل موجودی", "انتقال بین انبار" };

    [ObservableProperty] private ApiWarehouse? _selectedWarehouse;
    [ObservableProperty] private ApiWarehouse? _destWarehouse;     // مقصد انتقال
    [ObservableProperty] private string _operation = "رسید ورود";
    [ObservableProperty] private string _quickSearch = string.Empty;
    [ObservableProperty] private decimal _totalQty;
    [ObservableProperty] private decimal _totalValue;
    [ObservableProperty] private bool _isReceive = true;     // برای نمایش ستون بهای واحد
    [ObservableProperty] private bool _isTransfer;            // برای نمایش انتخاب انبار مقصد
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _barcodeInput = string.Empty;   // اسکنِ بارکدِ یکپارچه (#۲۷)
    [ObservableProperty] private string _scanInfo = string.Empty;

    public WarehouseClientViewModel(ApiClient api, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _api = api;
    }

    public override async Task LoadAsync()
    {
        Warehouses.Clear();
        foreach (var w in await _api.GetWarehousesAsync()) Warehouses.Add(w);
        SelectedWarehouse = Warehouses.FirstOrDefault();
        await RefreshStockAsync();
    }

    partial void OnOperationChanged(string value)
    {
        IsReceive = value == "رسید ورود";
        IsTransfer = value == "انتقال بین انبار";
    }
    partial void OnSelectedWarehouseChanged(ApiWarehouse? value) => _ = RefreshStockAsync();
    partial void OnQuickSearchChanged(string value) => _ = SearchAsync();

    [RelayCommand]
    private async Task SearchAsync()
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(QuickSearch)) return;
        var prods = await _api.SearchProductsAsync(QuickSearch);
        foreach (var p in prods.Take(40))
            SearchResults.Add(new WhProductTile(p.Id, p.Code, p.Name, p.SalePrice));
    }

    [RelayCommand]
    private void AddProduct(WhProductTile? tile)
    {
        if (tile is null || SelectedWarehouse is null) return;
        var existing = Cart.FirstOrDefault(c => c.ProductId == tile.Id);
        if (existing is not null) { existing.Qty++; }
        else
        {
            var line = new WhCartLine(tile.Id, tile.Code, tile.Name) { Qty = 1, UnitCost = 0 };
            line.PropertyChanged += (_, _) => Recalculate();
            Cart.Add(line);
        }
        Recalculate();
    }

    /// <summary>اسکن/تایپِ بارکد یا کدِ کالا (#۲۷) + Enter → افزودنِ مستقیم به سبدِ رسید/حواله/انتقال.</summary>
    [RelayCommand]
    private async Task ScanBarcodeAsync()
    {
        var code = BarcodeInput?.Trim() ?? "";
        BarcodeInput = string.Empty;
        if (string.IsNullOrEmpty(code)) return;
        if (SelectedWarehouse is null) { ScanInfo = "ابتدا انبار را انتخاب کنید."; return; }
        var hit = await _api.ResolveBarcodeAsync(code);
        if (hit is null) { ScanInfo = $"یافت نشد: {code}"; return; }
        AddProduct(new WhProductTile(hit.ProductId, hit.Code, hit.Name, hit.SalePrice));
        ScanInfo = $"✓ {hit.Name}";
    }

    [RelayCommand] private void Inc(WhCartLine? l) { if (l is not null) { l.Qty++; Recalculate(); } }
    [RelayCommand] private void Dec(WhCartLine? l) { if (l is not null) { if (l.Qty > 1) l.Qty--; else Cart.Remove(l); Recalculate(); } }
    [RelayCommand] private void RemoveLine(WhCartLine? l) { if (l is not null) { Cart.Remove(l); Recalculate(); } }
    [RelayCommand] private void Clear() { Cart.Clear(); Recalculate(); }

    private void Recalculate()
    {
        TotalQty = Cart.Sum(c => c.Qty);
        TotalValue = Cart.Sum(c => c.Qty * c.UnitCost);
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (SelectedWarehouse is null) { await _dialogService.ShowWarningAsync("انبار را انتخاب کنید."); return; }
        if (!Cart.Any()) { await _dialogService.ShowWarningAsync("سبد خالی است."); return; }
        var date = DateTime.Now.ToString("yyyy/MM/dd");

        await ExecuteAsync(async () =>
        {
            (bool ok, string? error) r;
            switch (Operation)
            {
                case "رسید ورود":
                    r = await _api.ReceiveStockAsync(SelectedWarehouse.Id, date, "رسید از کلاینت انبار",
                        Cart.Select(c => (c.ProductId, c.Qty, c.UnitCost)));
                    break;
                case "حواله خروج":
                    r = await _api.IssueStockAsync(SelectedWarehouse.Id, date, "حواله از کلاینت انبار",
                        Cart.Select(c => (c.ProductId, c.Qty)));
                    break;
                case "انتقال بین انبار":
                    if (DestWarehouse is null || DestWarehouse.Id == SelectedWarehouse.Id)
                    { await _dialogService.ShowWarningAsync("انبار مقصد را (متفاوت از مبدأ) انتخاب کنید."); return; }
                    r = (true, null);
                    foreach (var c in Cart)
                    {
                        var (ok, err) = await _api.TransferStockAsync(SelectedWarehouse.Id, DestWarehouse.Id,
                            c.ProductId, c.Qty, date, "انتقال از کلاینت انبار");
                        if (!ok) { r = (false, err); break; }
                    }
                    break;
                default: // تعدیل موجودی — هر ردیف موجودی را به مقدار واردشده تنظیم می‌کند
                    r = (true, null);
                    foreach (var c in Cart)
                    {
                        var (ok, err) = await _api.AdjustStockAsync(SelectedWarehouse.Id, c.ProductId, c.Qty, date, "تعدیل از کلاینت انبار");
                        if (!ok) { r = (false, err); break; }
                    }
                    break;
            }

            if (!r.ok) { await _dialogService.ShowErrorAsync(r.error ?? "خطا در ثبت سند."); return; }
            await _dialogService.ShowSuccessAsync($"«{Operation}» با موفقیت ثبت شد.");
            Cart.Clear(); Recalculate();
            await RefreshStockAsync();
        }, "در حال ثبت سند انبار...");
    }

    [RelayCommand]
    private async Task RefreshStockAsync()
    {
        Stock.Clear();
        if (SelectedWarehouse is null) return;
        foreach (var s in await _api.GetWarehouseStockAsync(SelectedWarehouse.Id))
            Stock.Add(s);
        StatusText = $"{Stock.Count} قلم در «{SelectedWarehouse.Name}»";
    }
}

public record WhProductTile(int Id, string Code, string Name, decimal Price);

public partial class WhCartLine : ObservableObject
{
    public int ProductId { get; }
    public string Code { get; }
    public string Name { get; }
    [ObservableProperty] private decimal _qty;
    [ObservableProperty] private decimal _unitCost;

    public WhCartLine(int productId, string code, string name)
    { ProductId = productId; Code = code; Name = name; }
}
