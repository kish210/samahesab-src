using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.Domain.Enums;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Inventory;

public partial class ProductEditViewModel : BaseViewModel, INavigationAware
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    /// <summary>نوعِ قلم: false=کالا، true=خدمت. «خدمات جدید» این را true می‌فرستد.</summary>
    [ObservableProperty] private bool _isService;
    public string FormTitle => IsService ? "خدماتِ جدید" : (IsEditing ? "ویرایشِ کالا" : "کالای جدید");
    partial void OnIsServiceChanged(bool value) => OnPropertyChanged(nameof(FormTitle));

    /// <summary>ناوبری با پارامترِ "service" → فرم در حالتِ خدمت باز می‌شود؛ با int → بارگذاریِ کالای موجود برایِ ویرایش.</summary>
    public async Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is string s && s.Equals("service", System.StringComparison.OrdinalIgnoreCase))
        {
            IsService = true;
            return;
        }
        if (parameter is int id && id > 0)
            await LoadForEditAsync(id);
    }

    private async Task LoadForEditAsync(int productId)
    {
        await ExecuteAsync(async () =>
        {
            var p = await _mediator.Send(new GetProductByIdQuery(productId));
            if (p is null) { await _dialogService.ShowErrorAsync("کالا یافت نشد."); return; }

            EditingProductId = p.Id;
            Code = p.Code;
            Barcode = p.Barcode ?? string.Empty;
            Name = p.Name;
            NameEn = p.NameEn;
            GroupId = p.GroupId;
            UnitId = p.UnitId;
            IsService = p.ProductType == ProductType.Service;
            PurchasePrice = p.PurchasePrice;
            SalePrice = p.SalePrice;
            WholesalePrice = p.WholesalePrice;
            ConsumerPrice = p.ConsumerPrice;
            TaxRate = p.TaxRate;
            MinStock = p.MinStock;
            MaxStock = p.MaxStock;
            HasSerial = p.HasSerial;
            HasBatch = p.HasBatch;
            HasExpiry = p.HasExpiry;
            ValuationMethod = p.ValuationMethod == Domain.Enums.ValuationMethod.FIFO ? "FIFO" : "میانگین";
            Description = p.Description;
            ImageBytes = p.Image;
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(FormTitle));
        }, "در حال بارگذاریِ کالا...");
    }

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
    [ObservableProperty] private byte[]? _imageBytes;

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
            var valuation = ValuationMethod == "FIFO" ? Domain.Enums.ValuationMethod.FIFO : Domain.Enums.ValuationMethod.WeightedAverage;
            var barcode = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode;

            if (IsEditing)
            {
                var updateResult = await _mediator.Send(new UpdateProductCommand(
                    ProductId: EditingProductId, Code: Code, Barcode: barcode,
                    Name: Name, NameEn: NameEn, GroupId: GroupId, UnitId: UnitId,
                    PurchasePrice: PurchasePrice, SalePrice: SalePrice,
                    WholesalePrice: WholesalePrice, ConsumerPrice: ConsumerPrice,
                    MinStock: MinStock, MaxStock: MaxStock,
                    HasSerial: HasSerial, HasBatch: HasBatch, HasExpiry: HasExpiry,
                    ValuationMethod: valuation, TaxRate: TaxRate, Description: Description,
                    Image: ImageBytes));
                if (updateResult.Succeeded)
                {
                    await _dialogService.ShowSuccessAsync(IsService ? "خدمت با موفقیت به‌روزرسانی شد." : "کالا با موفقیت به‌روزرسانی شد.");
                    _navigationService.NavigateTo("Products");
                }
                else await _dialogService.ShowErrorAsync(updateResult.ErrorMessage);
                return;
            }

            var command = new CreateProductCommand(
                Code: Code, Barcode: barcode,
                Name: Name, NameEn: NameEn, GroupId: GroupId, BrandId: null, UnitId: UnitId,
                ProductType: IsService ? ProductType.Service : ProductType.Product,
                PurchasePrice: PurchasePrice, SalePrice: SalePrice,
                WholesalePrice: WholesalePrice, ConsumerPrice: ConsumerPrice,
                MinStock: MinStock, MaxStock: MaxStock,
                HasSerial: HasSerial, HasBatch: HasBatch, HasExpiry: HasExpiry,
                ValuationMethod: valuation, TaxRate: TaxRate, Description: Description,
                Image: ImageBytes);

            var result = await _mediator.Send(command);
            if (result.Succeeded)
            {
                EditingProductId = result.Value;
                await _dialogService.ShowSuccessAsync(IsService ? "خدمت با موفقیت ذخیره شد." : "کالا با موفقیت ذخیره شد.");
                _navigationService.NavigateTo("Products");
            }
            else await _dialogService.ShowErrorAsync(result.ErrorMessage);
        });
    }

    [RelayCommand]
    private void ChooseImage()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "انتخابِ تصویرِ کالا",
            Filter = "تصویر (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
        };
        if (dlg.ShowDialog() == true)
            ImageBytes = System.IO.File.ReadAllBytes(dlg.FileName);
    }

    [RelayCommand] private void Cancel() => _navigationService.NavigateTo("Products");
}
