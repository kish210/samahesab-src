using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Inventory;

public partial class ProductListViewModel : BaseViewModel
{
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUserService _currentUser;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int? _selectedGroupId;
    [ObservableProperty] private bool _showInactive;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _lowStockCount;   // T16 — تعدادِ کالاهای زیرِ حداقل
    [ObservableProperty] private Product? _selectedProduct;

    public ObservableCollection<ProductListItem> Products { get; } = new();
    public List<ProductGroupItem> Groups { get; private set; } = new();

    public ProductListViewModel(
        IProductRepository productRepository,
        ICurrentUserService currentUser,
        IDialogService dialogService,
        INavigationService navigationService) : base(dialogService, navigationService)
    {
        _productRepository = productRepository;
        _currentUser = currentUser;
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
        var companyId = _currentUser.CompanyId!.Value;
        IReadOnlyList<Product> products;

        if (!string.IsNullOrWhiteSpace(SearchText))
            products = await _productRepository.SearchAsync(companyId, SearchText);
        else
            products = await _productRepository.GetAllAsync();

        // T16 — کالاهای زیرِ حداقلِ موجودی (برای بجِ هشدارِ کسری).
        var lowStockIds = (await _productRepository.GetLowStockAsync(companyId)).Select(p => p.Id).ToHashSet();

        Products.Clear();
        foreach (var p in products)
        {
            Products.Add(new ProductListItem(
                p.Id, p.Code, p.Barcode ?? "", p.Name,
                p.SalePrice, p.PurchasePrice, p.WholesalePrice,
                p.MinStock, p.IsActive, lowStockIds.Contains(p.Id)));
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
        _navigationService.NavigateTo("ProductEdit");
    }

    [RelayCommand]
    private async Task DeleteProductAsync()
    {
        if (SelectedProduct == null) return;

        var confirm = await _dialogService.ConfirmAsync(
            $"آیا از حذف کالا «{SelectedProduct.Name}» اطمینان دارید؟",
            "حذف کالا");
        if (!confirm) return;

        await ExecuteAsync(async () =>
        {
            SelectedProduct.Deactivate();
            _productRepository.Update(SelectedProduct);
            await LoadProductsAsync();
        });
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        await _dialogService.ShowInfoAsync("در حال آماده‌سازی فایل اکسل...");
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadProductsAsync();
}

public record ProductListItem(
    int Id, string Code, string Barcode, string Name,
    decimal SalePrice, decimal PurchasePrice, decimal WholesalePrice,
    decimal MinStock, bool IsActive, bool IsLowStock = false);

public record ProductGroupItem(int Id, string Name);
