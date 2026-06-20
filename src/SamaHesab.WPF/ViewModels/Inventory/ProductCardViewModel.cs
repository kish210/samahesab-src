using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>کارت کالا (۳۶۰°) — 🏛️ الگوی API-only: کلاینت→API، دسکتاپ→Application. بدونِ ریپازیتوریِ مستقیم.</summary>
public partial class ProductCardViewModel : BaseViewModel, INavigationAware
{
    /// <summary>CC-5 — بازکردنِ کارتِ یک کالای مشخص از فهرست (Param=Id). LoadAsync پیش از این صدا زده شده.</summary>
    public async Task OnNavigatedToAsync(object? parameter)
    {
        if (Products.Count == 0) await LoadAsync();
        if (parameter is int id && id > 0)
            SelectedProduct = Products.FirstOrDefault(p => p.Id == id) ?? SelectedProduct;
    }

    private readonly IMediator _mediator;
    private readonly ApiClient _api;

    [ObservableProperty] private ProductCardPick? _selectedProduct;

    // مشخصات
    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _barcode;
    [ObservableProperty] private bool _isActive;
    // قیمت
    [ObservableProperty] private decimal _purchasePrice;
    [ObservableProperty] private decimal _salePrice;
    [ObservableProperty] private decimal _wholesalePrice;
    [ObservableProperty] private decimal _consumerPrice;
    [ObservableProperty] private decimal _taxRate;
    [ObservableProperty] private decimal _retailMarginPct;
    // کنترل
    [ObservableProperty] private decimal _minStock;
    [ObservableProperty] private decimal? _maxStock;
    [ObservableProperty] private decimal? _reorderPoint;
    [ObservableProperty] private string _tracking = "ندارد";
    // موجودی
    [ObservableProperty] private decimal _totalStock;

    public List<ProductCardPick> Products { get; private set; } = new();
    public ObservableCollection<WarehouseStockRow> WarehouseStocks { get; } = new();
    public ObservableCollection<KardexRow> Kardex { get; } = new();

    public ProductCardViewModel(IMediator mediator, ApiClient api,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _api = api;
    }

    public override async Task LoadAsync()
    {
        // 🏛️ کلاینت→API، دسکتاپ→Application
        Products = (!string.IsNullOrWhiteSpace(_api.BaseUrl)
                ? (await _api.GetProductListAsync()).Select(p => new ProductCardPick(p.Id, $"{p.Code} — {p.Name}"))
                : (await _mediator.Send(new GetProductsQuery())).Select(p => new ProductCardPick(p.Id, $"{p.Code} — {p.Name}")))
            .ToList();
        OnPropertyChanged(nameof(Products));
        if (Products.Count > 0) SelectedProduct = Products[0];
    }

    partial void OnSelectedProductChanged(ProductCardPick? value)
    {
        if (value != null) _ = LoadCardAsync(value.Id);
    }

    private async Task LoadCardAsync(int productId)
    {
        await ExecuteAsync(async () =>
        {
            // 🏛️ مشخصات + موجودیِ چنداِنباره از کوئریِ تجمیعی (API/Application)
            ProductCardDto? p;
            if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
            {
                var a = await _api.GetProductCardAsync(productId);
                p = a == null ? null : new ProductCardDto(a.Id, a.Code, a.Name, a.Barcode, a.IsActive,
                    a.PurchasePrice, a.SalePrice, a.WholesalePrice, a.ConsumerPrice, a.TaxRate,
                    a.MinStock, a.MaxStock, a.ReorderPoint, a.Tracking, a.TotalStock,
                    a.WarehouseStocks.Select(s => new ProductCardStockRow(s.WarehouseName, s.Quantity, s.IsLow)).ToList());
            }
            else p = await _mediator.Send(new GetProductCardQuery(productId));
            if (p == null) return;

            Code = p.Code; Name = p.Name; Barcode = p.Barcode; IsActive = p.IsActive;
            PurchasePrice = p.PurchasePrice; SalePrice = p.SalePrice;
            WholesalePrice = p.WholesalePrice; ConsumerPrice = p.ConsumerPrice; TaxRate = p.TaxRate;
            RetailMarginPct = p.PurchasePrice > 0
                ? System.Math.Round((p.SalePrice - p.PurchasePrice) / p.PurchasePrice * 100, 1) : 0;
            MinStock = p.MinStock; MaxStock = p.MaxStock; ReorderPoint = p.ReorderPoint;
            Tracking = p.Tracking;

            WarehouseStocks.Clear();
            foreach (var s in p.WarehouseStocks)
                WarehouseStocks.Add(new WarehouseStockRow(s.WarehouseName, s.Quantity, s.IsLow));
            TotalStock = p.TotalStock;

            // کاردکس (کوئریِ موجود)
            var rows = await _mediator.Send(new GetKardexQuery(productId, null, null, null));
            Kardex.Clear();
            foreach (var r in rows) Kardex.Add(r);
        }, "در حال بارگذاری کارت کالا...");
    }

    [RelayCommand] private async Task RefreshAsync()
    {
        if (SelectedProduct != null) await LoadCardAsync(SelectedProduct.Id);
    }
}

public record ProductCardPick(int Id, string Display);
public record WarehouseStockRow(string WarehouseName, decimal Quantity, bool IsLow);
