using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Domain.Enums;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Inventory;

public partial class ProductEditViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _barcode = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _nameEn;
    [ObservableProperty] private int? _groupId;
    [ObservableProperty] private int _unitId = 1;
    [ObservableProperty] private decimal _salePrice;
    [ObservableProperty] private decimal _purchasePrice;
    [ObservableProperty] private decimal _wholesalePrice;
    [ObservableProperty] private decimal _consumerPrice;
    [ObservableProperty] private decimal _taxRate;
    [ObservableProperty] private decimal _minStock;
    [ObservableProperty] private decimal? _maxStock;
    [ObservableProperty] private bool _hasSerial;
    [ObservableProperty] private bool _hasBatch;
    [ObservableProperty] private bool _hasExpiry;
    [ObservableProperty] private string _valuationMethod = "میانگین";
    [ObservableProperty] private string? _description;
    [ObservableProperty] private int _editingProductId;

    public bool IsEditing => EditingProductId > 0;
    public List<string> ValuationMethods { get; } = new() { "میانگین", "FIFO" };

    public ProductEditViewModel(IMediator mediator, ICurrentUserService currentUser,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await ExecuteAsync(async () =>
        {
            var command = new CreateProductCommand(
                Code: Code, Barcode: string.IsNullOrWhiteSpace(Barcode) ? null : Barcode,
                Name: Name, NameEn: NameEn, GroupId: GroupId, BrandId: null, UnitId: UnitId,
                ProductType: ProductType.Product,
                PurchasePrice: PurchasePrice, SalePrice: SalePrice,
                WholesalePrice: WholesalePrice, ConsumerPrice: ConsumerPrice,
                MinStock: MinStock, MaxStock: MaxStock,
                HasSerial: HasSerial, HasBatch: HasBatch, HasExpiry: HasExpiry,
                ValuationMethod: ValuationMethod == "FIFO" ? Domain.Enums.ValuationMethod.FIFO : Domain.Enums.ValuationMethod.WeightedAverage,
                TaxRate: TaxRate, Description: Description);

            var result = await _mediator.Send(command);
            if (result.Succeeded)
            {
                EditingProductId = result.Value;
                await _dialogService.ShowSuccessAsync("کالا با موفقیت ذخیره شد.");
                _navigationService.NavigateTo("Products");
            }
            else await _dialogService.ShowErrorAsync(result.ErrorMessage);
        });
    }

    [RelayCommand] private void Cancel() => _navigationService.NavigateTo("Products");
}
