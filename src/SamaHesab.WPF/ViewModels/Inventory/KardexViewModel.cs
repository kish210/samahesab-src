using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Inventory;

public partial class KardexViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IProductRepository _productRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private ProductPick? _selectedProduct;
    [ObservableProperty] private int? _selectedWarehouseId;   // null = همه انبارها
    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private decimal _totalIn;
    [ObservableProperty] private decimal _totalOut;

    public List<WarehousePick> Warehouses { get; private set; } = new();
    public List<ProductPick> Products { get; private set; } = new();
    public ObservableCollection<KardexRow> Rows { get; } = new();

    public KardexViewModel(IMediator mediator, IWarehouseRepository warehouseRepo,
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
        var cal = new System.Globalization.PersianCalendar();
        var now = DateTime.Now;
        FromDate = $"{cal.GetYear(now)}/01/01";
        ToDate = _calendar.GetCurrentPersianDate();

        var whs = await _warehouseRepo.GetByCompanyAsync(companyId);
        Warehouses = new List<WarehousePick> { new(0, "همه انبارها") }
            .Concat(whs.Select(w => new WarehousePick(w.Id, w.Name))).ToList();
        OnPropertyChanged(nameof(Warehouses));

        var prods = await _productRepo.SearchAsync(companyId, "");
        Products = prods.Select(p => new ProductPick(p.Id, $"{p.Code} - {p.Name}")).ToList();
        OnPropertyChanged(nameof(Products));
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (SelectedProduct == null) { await _dialogService.ShowErrorAsync("کالا را انتخاب کنید."); return; }
        await ExecuteAsync(async () =>
        {
            Rows.Clear();
            var wh = (SelectedWarehouseId is null or 0) ? (int?)null : SelectedWarehouseId;
            var rows = await _mediator.Send(new GetKardexQuery(SelectedProduct.Id, wh, FromDate, ToDate));
            foreach (var r in rows) Rows.Add(r);
            TotalIn = rows.Sum(r => r.In);
            TotalOut = rows.Sum(r => r.Out);
        }, "در حال تهیه کاردکس...");
    }
}
