using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Modules.Tourism.Application.Commands;
using SamaHesab.Modules.Tourism.Application.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Tourism;

/// <summary>
/// TUR-C2-6 — مدیریتِ محصولات/خدماتِ گردشگری (master-detail). تعریفِ محصول با تأمین‌کننده
/// (از اشخاص)، قیمتِ خرید/فروشِ پیش‌فرض، نیاز به لیستِ مسافر و فعال/غیرفعال.
/// همان محصولاتی که در صفحهٔ «ثبتِ فروشِ گردشگری» انتخاب می‌شوند.
/// </summary>
public partial class TourismProductsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    [ObservableProperty] private TourismProductDto? _selectedProduct;

    // فیلدهای فرمِ ویرایش/افزودن
    [ObservableProperty] private int _editId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private int _supplierPartyId;
    [ObservableProperty] private decimal _purchasePrice;
    [ObservableProperty] private decimal _defaultSalePrice;
    [ObservableProperty] private bool _requiresPassengerList;
    [ObservableProperty] private bool _active = true;

    public ObservableCollection<TourismProductDto> Products { get; } = new();
    public ObservableCollection<SupplierRowDto> Suppliers { get; } = new();

    public TourismProductsViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            Suppliers.Clear();
            foreach (var s in await _mediator.Send(new GetSuppliersQuery())) Suppliers.Add(s);
            await ReloadProductsAsync();
        }, "در حال بارگذاری...");
    }

    private async Task ReloadProductsAsync()
    {
        Products.Clear();
        // ActiveOnly=false تا غیرفعال‌ها هم در مدیریت دیده شوند.
        foreach (var p in await _mediator.Send(new GetTourismProductsQuery(ActiveOnly: false))) Products.Add(p);
    }

    partial void OnSelectedProductChanged(TourismProductDto? value)
    {
        if (value is null) return;
        EditId = value.Id; Name = value.Name; SupplierPartyId = value.SupplierPartyId;
        PurchasePrice = value.PurchasePrice; DefaultSalePrice = value.DefaultSalePrice;
        RequiresPassengerList = value.RequiresPassengerList; Active = value.Active;
    }

    /// <summary>فرمِ خالی برای محصولِ نو (F2).</summary>
    [RelayCommand]
    private void New()
    {
        SelectedProduct = null;
        EditId = 0; Name = string.Empty; SupplierPartyId = 0;
        PurchasePrice = 0; DefaultSalePrice = 0; RequiresPassengerList = false; Active = true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new SaveTourismProductCommand(
                EditId, Name?.Trim() ?? string.Empty, SupplierPartyId, PurchasePrice, DefaultSalePrice,
                ProductGroupId: null, RequiresPassengerList, Active));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync(EditId > 0 ? "محصول ویرایش شد." : "محصولِ نو ثبت شد.");
            await ReloadProductsAsync();
            New();
        }, "در حال ذخیره...");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (EditId <= 0) { await _dialogService.ShowWarningAsync("ابتدا محصولی را از فهرست انتخاب کنید."); return; }
        if (!await _dialogService.ConfirmAsync($"محصولِ «{Name}» غیرفعال شود؟", "حذف محصول")) return;
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new DeleteTourismProductCommand(EditId));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync("محصول غیرفعال شد.");
            await ReloadProductsAsync();
            New();
        }, "در حال حذف...");
    }
}
