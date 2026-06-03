using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Inventory;

public partial class StockTransferViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IProductRepository _productRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private int _fromWarehouseId;
    [ObservableProperty] private int _toWarehouseId;
    [ObservableProperty] private ProductPick? _selectedProduct;
    [ObservableProperty] private decimal _quantity = 1;
    [ObservableProperty] private string _transferDate = string.Empty;
    [ObservableProperty] private string? _description;

    public List<WarehousePick> Warehouses { get; private set; } = new();
    public List<ProductPick> Products { get; private set; } = new();

    public StockTransferViewModel(IMediator mediator, IWarehouseRepository warehouseRepo,
        IProductRepository productRepo, ICurrentUserService currentUser,
        IPersianCalendarService calendar, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _warehouseRepo = warehouseRepo; _productRepo = productRepo;
        _currentUser = currentUser; _calendar = calendar;
    }

    public override async Task LoadAsync()
    {
        var companyId = _currentUser.CompanyId ?? 1;
        TransferDate = _calendar.GetCurrentPersianDate();
        var whs = await _warehouseRepo.GetByCompanyAsync(companyId);
        Warehouses = whs.Select(w => new WarehousePick(w.Id, w.Name)).ToList();
        OnPropertyChanged(nameof(Warehouses));
        if (Warehouses.Count > 0) FromWarehouseId = Warehouses[0].Id;
        if (Warehouses.Count > 1) ToWarehouseId = Warehouses[1].Id;

        var prods = await _productRepo.SearchAsync(companyId, "");
        Products = prods.Select(p => new ProductPick(p.Id, $"{p.Code} - {p.Name}")).ToList();
        OnPropertyChanged(nameof(Products));
    }

    [RelayCommand]
    private async Task TransferAsync()
    {
        if (SelectedProduct == null) { await _dialogService.ShowErrorAsync("کالا را انتخاب کنید."); return; }
        if (FromWarehouseId == ToWarehouseId) { await _dialogService.ShowErrorAsync("انبار مبدأ و مقصد یکسان است."); return; }
        await ExecuteAsync(async () =>
        {
            var result = await _mediator.Send(new TransferStockCommand(
                FromWarehouseId, ToWarehouseId, SelectedProduct.Id, Quantity, TransferDate, Description));
            if (result.Succeeded)
            {
                await _dialogService.ShowSuccessAsync("انتقال انبار با موفقیت ثبت شد.");
                Quantity = 1; SelectedProduct = null; Description = null;
            }
            else await _dialogService.ShowErrorAsync(result.ErrorMessage);
        }, "در حال ثبت انتقال...");
    }
}

public record WarehousePick(int Id, string Name);
public record ProductPick(int Id, string Display);
