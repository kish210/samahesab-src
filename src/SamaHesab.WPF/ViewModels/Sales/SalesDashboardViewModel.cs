using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.BI.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Sales;

/// <summary>
/// داشبوردِ اختصاصیِ فروشنده — فروشِ امروز/ماهِ خودِ کاربر + طلبِ نوصول + فاکتورهای معوق.
/// روی کوئریِ آماده‌ی <see cref="GetSalesRepDashboardQuery"/> (فیلتر بر SalesRepId == کاربرِ جاری).
/// </summary>
public partial class SalesDashboardViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;
    private readonly ICurrentUserService _currentUser;

    [ObservableProperty] private string _todayText = string.Empty;
    [ObservableProperty] private string _repName = string.Empty;

    // ── امروز ──
    [ObservableProperty] private decimal _todaySales;
    [ObservableProperty] private int _todayCount;
    [ObservableProperty] private decimal _todayAverage;

    // ── این ماه ──
    [ObservableProperty] private decimal _monthSales;
    [ObservableProperty] private int _monthCount;

    // ── طلب/معوق ──
    [ObservableProperty] private int _unpaidCount;
    [ObservableProperty] private decimal _unpaidTotal;
    [ObservableProperty] private int _overdueCount;
    [ObservableProperty] private decimal _overdueTotal;

    public SalesDashboardViewModel(IMediator mediator, IPersianCalendarService calendar,
        ICurrentUserService currentUser, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; _currentUser = currentUser; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var today = _calendar.GetCurrentPersianDate();
            TodayText = today;
            RepName = _currentUser.FullName ?? _currentUser.Username ?? "فروشنده";
            // شروعِ ماهِ جاری شمسی: «yyyy/mm/01».
            var monthStart = (today.Length >= 7 ? today.Substring(0, 7) : today.PadRight(7, '0')) + "/01";

            var d = await _mediator.Send(new GetSalesRepDashboardQuery(today, monthStart));
            TodaySales = d.TodaySales; TodayCount = d.TodayCount; TodayAverage = d.TodayAverage;
            MonthSales = d.MonthSales; MonthCount = d.MonthCount;
            UnpaidCount = d.UnpaidCount; UnpaidTotal = d.UnpaidTotal;
            OverdueCount = d.OverdueCount; OverdueTotal = d.OverdueTotal;
        }, "در حال بارگذاری داشبورد فروش...");
    }

    [RelayCommand] private Task RefreshAsync() => LoadAsync();

    /// <summary>صدورِ فاکتور فروشِ جدید از داشبورد.</summary>
    [RelayCommand] private void NewInvoice() => _navigationService.NavigateTo("SalesInvoice");

    /// <summary>رفتن به فهرستِ فاکتورهای فروش (پیگیریِ طلب/معوق).</summary>
    [RelayCommand] private void OpenInvoices() => _navigationService.NavigateTo("SalesInvoiceList");
}
