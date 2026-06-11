using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>کارت کالا (نمای ۳۶۰°): موجودی چند‌انباره + مشخصات/قیمت/کنترل + کاردکس — PD-6.</summary>
public partial class ProductCardViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IProductRepository _productRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IRepository<StockItem> _stockRepo;
    private readonly ICurrentUserService _currentUser;

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

    public ProductCardViewModel(IMediator mediator, IProductRepository productRepo,
        IWarehouseRepository warehouseRepo, IRepository<StockItem> stockRepo,
        ICurrentUserService currentUser, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _productRepo = productRepo; _warehouseRepo = warehouseRepo;
        _stockRepo = stockRepo; _currentUser = currentUser;
    }

    public override async Task LoadAsync()
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var prods = await _productRepo.SearchAsync(companyId, "");
        Products = prods.Select(p => new ProductCardPick(p.Id, $"{p.Code} — {p.Name}")).ToList();
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
            var companyId = _currentUser.CompanyId ?? 1;
            var p = await _productRepo.GetByIdAsync(productId);
            if (p == null) return;

            Code = p.Code; Name = p.Name; Barcode = p.Barcode; IsActive = p.IsActive;
            PurchasePrice = p.PurchasePrice; SalePrice = p.SalePrice;
            WholesalePrice = p.WholesalePrice; ConsumerPrice = p.ConsumerPrice; TaxRate = p.TaxRate;
            RetailMarginPct = p.PurchasePrice > 0
                ? System.Math.Round((p.SalePrice - p.PurchasePrice) / p.PurchasePrice * 100, 1) : 0;
            MinStock = p.MinStock; MaxStock = p.MaxStock; ReorderPoint = p.ReorderPoint;
            Tracking = p.HasSerial ? "سریال" : p.HasBatch ? "بچ" : "ندارد";

            // موجودی per-انبار
            var whs = await _warehouseRepo.GetByCompanyAsync(companyId);
            var whName = whs.ToDictionary(w => w.Id, w => w.Name);
            var stock = await _stockRepo.FindAsync(s => s.ProductId == productId);
            WarehouseStocks.Clear();
            foreach (var g in stock.GroupBy(s => s.WarehouseId))
            {
                var qty = g.Sum(x => x.Quantity);
                WarehouseStocks.Add(new WarehouseStockRow(
                    whName.TryGetValue(g.Key, out var n) ? n : $"#{g.Key}", qty, qty < p.MinStock));
            }
            TotalStock = stock.Sum(s => s.Quantity);

            // کاردکس
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
