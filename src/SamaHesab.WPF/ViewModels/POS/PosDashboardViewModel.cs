using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.BI.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.POS;

/// <summary>
/// F9-1 — داشبوردِ عملیاتیِ صندوق/رستوران: KPIهای فروشِ امروز + وضعیتِ میزها/سفارش‌های باز.
/// روی کوئری‌های آماده‌ی `GetCashierDashboardQuery`/`GetRestaurantDashboardQuery` (بک‌اندِ BI).
/// </summary>
public partial class PosDashboardViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private string _todayText = string.Empty;

    // ── صندوق (Cashier) ──
    [ObservableProperty] private decimal _todaySales;
    [ObservableProperty] private int _todayInvoiceCount;
    [ObservableProperty] private decimal _averageInvoice;

    // ── رستوران (اختیاری) ──
    [ObservableProperty] private bool _hasRestaurant;
    [ObservableProperty] private int _freeTables;
    [ObservableProperty] private int _occupiedTables;
    [ObservableProperty] private int _reservedTables;
    [ObservableProperty] private int _billingTables;
    [ObservableProperty] private int _openOrders;
    [ObservableProperty] private decimal _openOrdersValue;

    public PosDashboardViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var today = _calendar.GetCurrentPersianDate();
            TodayText = today;

            var cash = await _mediator.Send(new GetCashierDashboardQuery(today));
            TodaySales = cash.TodaySales;
            TodayInvoiceCount = cash.TodayInvoiceCount;
            AverageInvoice = cash.AverageInvoice;

            try
            {
                var r = await _mediator.Send(new GetRestaurantDashboardQuery());
                FreeTables = r.FreeTables; OccupiedTables = r.OccupiedTables;
                ReservedTables = r.ReservedTables; BillingTables = r.BillingTables;
                OpenOrders = r.OpenOrders; OpenOrdersValue = r.OpenOrdersValue;
                HasRestaurant = (FreeTables + OccupiedTables + ReservedTables + BillingTables) > 0 || OpenOrders > 0;
            }
            catch { HasRestaurant = false; }
        }, "در حال بارگذاری داشبورد صندوق...");
    }

    [RelayCommand] private Task RefreshAsync() => LoadAsync();
}
