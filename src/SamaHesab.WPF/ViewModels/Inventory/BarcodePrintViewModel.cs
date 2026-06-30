using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>
/// L2 — چاپِ بارکدِ کالا (تکی/تعدادی) + ساختِ بارکد. کالا را انتخاب می‌کند، اگر بارکد نداشت
/// «تولیدِ بارکد» یک مقدارِ یکتا می‌سازد، و برچسب‌ها (نام/کد/قیمت) را برای چاپ آماده می‌کند.
/// رندرِ Code128 و چاپ در BarcodeService/code-behind است؛ این VM فقط داده/وضعیت را نگه می‌دارد.
/// </summary>
public partial class BarcodePrintViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ApiClient _api;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private ProductListItem? _selectedProduct;
    [ObservableProperty] private string _barcodeValue = string.Empty;
    [ObservableProperty] private string _productName = string.Empty;
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private bool _showName = true;
    [ObservableProperty] private bool _showPrice = true;
    [ObservableProperty] private int _count = 1;
    [ObservableProperty] private int _columns = 3;

    public ObservableCollection<ProductListItem> Products { get; } = new();
    private readonly System.Collections.Generic.List<ProductListItem> _all = new();

    /// <summary>قیمتِ قالب‌بندی‌شده برای برچسب.</summary>
    public string PriceText => Price > 0 ? $"{Price:#,##0} ریال" : string.Empty;

    public BarcodePrintViewModel(IMediator mediator, ApiClient api, IDialogService d, INavigationService n)
        : base(d, n) { _mediator = mediator; _api = api; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            _all.Clear();
            var rows = !string.IsNullOrWhiteSpace(_api.BaseUrl)
                ? (await _api.GetProductListAsync(null)).Select(p => new ProductListItem(p.Id, p.Code, p.Barcode, p.Name, p.SalePrice, p.PurchasePrice, p.WholesalePrice, p.MinStock, p.IsActive, p.IsLowStock))
                : (await _mediator.Send(new GetProductsQuery(null))).Select(p => new ProductListItem(p.Id, p.Code, p.Barcode, p.Name, p.SalePrice, p.PurchasePrice, p.WholesalePrice, p.MinStock, p.IsActive, p.IsLowStock));
            foreach (var p in rows) _all.Add(p);
            ApplyFilter();
        }, "در حال بارگذاری کالاها...");
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var t = SearchText?.Trim() ?? "";
        Products.Clear();
        foreach (var p in _all.Where(p => t.Length == 0 || p.Name.Contains(t) || p.Code.Contains(t) || (p.Barcode ?? "").Contains(t)).Take(200))
            Products.Add(p);
    }

    partial void OnSelectedProductChanged(ProductListItem? value)
    {
        if (value is null) return;
        ProductName = value.Name;
        Price = value.SalePrice;
        BarcodeValue = string.IsNullOrWhiteSpace(value.Barcode) ? value.Code : value.Barcode;
        OnPropertyChanged(nameof(PriceText));
    }

    partial void OnPriceChanged(decimal value) => OnPropertyChanged(nameof(PriceText));

    /// <summary>ساختِ بارکد: اگر کالا بارکد ندارد، یک کدِ عددیِ یکتا (بر پایهٔ کدِ کالا/زمان) تولید می‌کند.</summary>
    [RelayCommand]
    private void GenerateBarcode()
    {
        var baseCode = SelectedProduct?.Code ?? "";
        var digits = new string(baseCode.Where(char.IsDigit).ToArray());
        if (digits.Length < 6) digits = (digits + System.DateTime.Now.ToString("yyMMddHHmmss")).PadLeft(12, '0');
        BarcodeValue = digits.Length > 12 ? digits.Substring(0, 12) : digits;
    }

    // افزایش/کاهشِ سریعِ تعداد (دکمه‌های +/−)
    [RelayCommand] private void IncCount() => Count = Count + 1;
    [RelayCommand] private void DecCount() { if (Count > 1) Count = Count - 1; }
}
