using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>کاردکس — 🏛️ الگوی API-only: داده و دراپ‌داون‌ها از API (کلاینت) یا Application (دسکتاپ). بدونِ ریپازیتوریِ مستقیم.</summary>
public partial class KardexViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ApiClient _api;
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

    public KardexViewModel(IMediator mediator, ApiClient api, ICurrentUserService currentUser,
        IPersianCalendarService calendar, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _api = api;
        _currentUser = currentUser; _calendar = calendar;
    }

    public override async Task LoadAsync()
    {
        var cal = new System.Globalization.PersianCalendar();
        var now = DateTime.Now;
        FromDate = $"{cal.GetYear(now)}/01/01";
        ToDate = _calendar.GetCurrentPersianDate();

        var whPicks = new List<WarehousePick> { new(0, "همه انبارها") };
        if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
        {
            whPicks.AddRange((await _api.GetWarehousesAsync()).Select(w => new WarehousePick(w.Id, w.Name)));
            Products = (await _api.GetProductListAsync()).Select(p => new ProductPick(p.Id, $"{p.Code} - {p.Name}")).ToList();
        }
        else
        {
            whPicks.AddRange((await _mediator.Send(new GetWarehousesQuery())).Select(w => new WarehousePick(w.Id, w.Name)));
            Products = (await _mediator.Send(new GetProductsQuery())).Select(p => new ProductPick(p.Id, $"{p.Code} - {p.Name}")).ToList();
        }
        Warehouses = whPicks;
        OnPropertyChanged(nameof(Warehouses));
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
