using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.Application.Reports.Export;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>
/// مدیریت کالا — 🏛️ الگوی API-only: کلاینتِ شبکه‌ای از API (ApiClient.GetProductList)،
/// دسکتاپِ آفلاین از لایهٔ Application (GetProductsQuery). بدونِ ریپازیتوریِ مستقیم در ViewModel.
/// </summary>
public partial class ProductListViewModel : BaseViewModel, SamaHesab.WPF.Services.ISupportsNew
{
    /// <summary>F2-GLOBAL: «جدید» → ساختِ کالای جدید.</summary>
    public void RequestNew() => NewProduct();

    private readonly IMediator _mediator;
    private readonly ApiClient _api;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int? _selectedGroupId;
    [ObservableProperty] private bool _showInactive;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _lowStockCount;   // T16 — تعدادِ کالاهای زیرِ حداقل
    [ObservableProperty] private ProductListItem? _selectedProduct;

    public ObservableCollection<ProductListItem> Products { get; } = new();
    public List<ProductGroupItem> Groups { get; private set; } = new();

    public ProductListViewModel(IMediator mediator, ApiClient api,
        IDialogService dialogService,
        INavigationService navigationService) : base(dialogService, navigationService)
    {
        _mediator = mediator;
        _api = api;
    }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            await LoadGroupsAsync();
            await LoadProductsAsync();
        }, "در حال بارگذاری کالاها...");
    }

    private async Task LoadGroupsAsync()
    {
        Groups = new List<ProductGroupItem>
        {
            new(0, "همه گروه‌ها"),
            new(1, "مواد غذایی"),
            new(2, "لوازم خانگی"),
            new(3, "پوشاک"),
        };
        await Task.CompletedTask;
    }

    private async Task LoadProductsAsync()
    {
        Products.Clear();
        var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;

        // 🏛️ مسیرِ داده: کلاینتِ شبکه‌ای → API؛ دسکتاپِ آفلاین → Application (بدونِ ریپازیتوریِ مستقیم).
        if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
        {
            foreach (var p in await _api.GetProductListAsync(search))
                Products.Add(new ProductListItem(p.Id, p.Code, p.Barcode, p.Name,
                    p.SalePrice, p.PurchasePrice, p.WholesalePrice, p.MinStock, p.IsActive, p.IsLowStock));
        }
        else
        {
            foreach (var p in await _mediator.Send(new GetProductsQuery(search)))
                Products.Add(new ProductListItem(p.Id, p.Code, p.Barcode, p.Name,
                    p.SalePrice, p.PurchasePrice, p.WholesalePrice, p.MinStock, p.IsActive, p.IsLowStock));
        }

        TotalCount = Products.Count;
        ActiveCount = Products.Count(p => p.IsActive);
        LowStockCount = Products.Count(p => p.IsLowStock);
    }

    [RelayCommand]
    private async Task SearchAsync() => await LoadProductsAsync();

    [RelayCommand]
    private void NewProduct()
    {
        _navigationService.NavigateTo("ProductEdit");
    }

    [RelayCommand]
    private void EditProduct()
    {
        if (SelectedProduct == null) return;
        _navigationService.NavigateTo("ProductEdit", SelectedProduct.Id);
    }

    /// <summary>CC-5 — کارتِ ۳۶۰°ِ کالای انتخاب‌شده (راست‌کلیک/دابل‌کلیک).</summary>
    [RelayCommand]
    private void OpenCard(ProductListItem? p)
    {
        var item = p ?? SelectedProduct;
        if (item != null) _navigationService.NavigateTo("ProductCard", item.Id);
    }

    [RelayCommand]
    private async Task DeleteProductAsync()
    {
        if (SelectedProduct == null) return;

        var confirm = await _dialogService.ConfirmAsync(
            $"آیا از حذف کالا «{SelectedProduct.Name}» اطمینان دارید؟",
            "حذف کالا");
        if (!confirm) return;

        var id = SelectedProduct.Id;
        await ExecuteAsync(async () =>
        {
            // 🏛️ حذفِ نرم از طریقِ API یا لایهٔ Application — نه ریپازیتوریِ مستقیم.
            if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
                await _api.DeactivateProductAsync(id);
            else
                await _mediator.Send(new DeactivateProductCommand(id));
            await LoadProductsAsync();
        });
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (Products.Count == 0) { await _dialogService.ShowWarningAsync("فهرستی برای خروجی نیست."); return; }
        try
        {
            string N(decimal d) => d.ToString("N0");
            var table = new ReportTable("لیست کالاها",
                new[] { "کد", "بارکد", "نام کالا", "قیمت فروش", "قیمت خرید", "حداقل موجودی", "وضعیت", "هشدار" },
                Products.Select(p => new[] { p.Code, p.Barcode, p.Name, N(p.SalePrice), N(p.PurchasePrice),
                    N(p.MinStock), p.IsActive ? "فعال" : "غیرفعال", p.IsLowStock ? "کسری" : "" }).ToList());
            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"لیست_کالاها_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
            System.IO.File.WriteAllText(path, ReportExporter.ToCsv(table), new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadProductsAsync();
}

public record ProductListItem(
    int Id, string Code, string Barcode, string Name,
    decimal SalePrice, decimal PurchasePrice, decimal WholesalePrice,
    decimal MinStock, bool IsActive, bool IsLowStock = false);

public record ProductGroupItem(int Id, string Name);
