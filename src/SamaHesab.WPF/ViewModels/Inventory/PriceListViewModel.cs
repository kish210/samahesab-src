using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>
/// کارِ ۷ (هستهٔ ERP) — مدیریتِ لیست‌قیمت: گریدِ همهٔ کالاها با ۴ سطحِ قیمتِ قابل‌ویرایش
/// (خرید/خرده/عمده/مصرف‌کننده) + عملیاتِ گروهیِ «اعمالِ ٪ روی یک سطح» + ذخیرهٔ دسته‌ای.
/// از فیلدهای موجودِ `Product` استفاده می‌کند (بدونِ موجودیتِ جدید).
/// </summary>
public partial class PriceListViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ApiClient _api;

    public ObservableCollection<PriceRow> Rows { get; } = new();
    public List<string> PriceLevels { get; } = new() { "قیمت خرید", "خرده‌فروشی", "عمده", "مصرف‌کننده" };

    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private string _selectedPriceLevel = "خرده‌فروشی";
    [ObservableProperty] private decimal _bulkPercent;
    [ObservableProperty] private int _dirtyCount;

    public PriceListViewModel(IMediator mediator, ApiClient api,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _api = api; }

    private async Task<List<ProductRowDto>> FetchProductsAsync(string? search)
    {
        if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
            return (await _api.GetProductListAsync(search))
                .Select(p => new ProductRowDto(p.Id, p.Code, p.Barcode, p.Name, p.SalePrice, p.PurchasePrice,
                    p.WholesalePrice, p.MinStock, p.IsActive, p.IsLowStock, p.ConsumerPrice, p.TaxRate)).ToList();
        return await _mediator.Send(new GetProductsQuery(search));
    }

    public override async Task LoadAsync() => await RunSearchAsync();

    [RelayCommand]
    private async Task RunSearchAsync()
    {
        await ExecuteAsync(async () =>
        {
            var list = await FetchProductsAsync(Search?.Trim());
            Rows.Clear();
            foreach (var p in list)
            {
                var row = new PriceRow
                {
                    ProductId = p.Id, Code = p.Code, Name = p.Name,
                    PurchasePrice = p.PurchasePrice, SalePrice = p.SalePrice,
                    WholesalePrice = p.WholesalePrice, ConsumerPrice = p.ConsumerPrice
                };
                row.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(PriceRow.IsDirty)) { row.IsDirty = true; RecountDirty(); }
                };
                Rows.Add(row);
            }
            RecountDirty();
        }, "در حال بارگذاریِ لیست‌قیمت...");
    }

    private void RecountDirty() => DirtyCount = Rows.Count(r => r.IsDirty);

    /// <summary>اعمالِ درصدِ تغییر روی سطحِ قیمتِ انتخاب‌شده برای همهٔ ردیف‌های نمایش‌داده‌شده.</summary>
    [RelayCommand]
    private void ApplyBulk()
    {
        if (BulkPercent == 0) return;
        var factor = 1 + BulkPercent / 100m;
        foreach (var r in Rows)
        {
            switch (SelectedPriceLevel)
            {
                case "قیمت خرید":   r.PurchasePrice  = System.Math.Round(r.PurchasePrice  * factor); break;
                case "عمده":        r.WholesalePrice = System.Math.Round(r.WholesalePrice * factor); break;
                case "مصرف‌کننده":  r.ConsumerPrice  = System.Math.Round(r.ConsumerPrice  * factor); break;
                default:            r.SalePrice      = System.Math.Round(r.SalePrice      * factor); break;
            }
        }
        RecountDirty();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var dirty = Rows.Where(r => r.IsDirty).ToList();
        if (dirty.Count == 0) { await _dialogService.ShowInfoAsync("تغییری برای ذخیره نیست."); return; }

        await ExecuteAsync(async () =>
        {
            int ok = 0; string? firstError = null;
            foreach (var r in dirty)
            {
                var res = await _mediator.Send(new UpdateProductPricesCommand(
                    r.ProductId, r.PurchasePrice, r.SalePrice, r.WholesalePrice, r.ConsumerPrice));
                if (res.Succeeded) { r.IsDirty = false; ok++; }
                else firstError ??= res.ErrorMessage;
            }
            RecountDirty();
            if (firstError != null) await _dialogService.ShowErrorAsync($"{ok} کالا ذخیره شد؛ خطا: {firstError}");
            else await _dialogService.ShowSuccessAsync($"قیمتِ {ok} کالا ذخیره شد.");
        }, "در حال ذخیرهٔ قیمت‌ها...");
    }
}

public partial class PriceRow : ObservableObject
{
    public int ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    [ObservableProperty] private decimal _purchasePrice;
    [ObservableProperty] private decimal _salePrice;
    [ObservableProperty] private decimal _wholesalePrice;
    [ObservableProperty] private decimal _consumerPrice;
    [ObservableProperty] private bool _isDirty;
}
