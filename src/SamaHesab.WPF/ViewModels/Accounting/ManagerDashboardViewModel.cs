using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.BI.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Accounting;

/// <summary>
/// F9-8 — داشبورد مدیریتی (مدیر/مالک): KPIهای کسب‌وکار روی `GetManagerDashboardQuery`ِ آماده
/// (فروشِ امروز/ماه · سود و حاشیه · دریافتنی/پرداختنی · چکِ در جریان + مشتریانِ برتر).
/// </summary>
public partial class ManagerDashboardViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private string _todayText = string.Empty;
    [ObservableProperty] private decimal _todaySales;
    [ObservableProperty] private decimal _monthSales;
    [ObservableProperty] private decimal _monthProfit;
    [ObservableProperty] private decimal _monthMarginPercent;
    [ObservableProperty] private decimal _receivablesTotal;
    [ObservableProperty] private decimal _payablesTotal;
    [ObservableProperty] private int _chequesInProcess;

    public ObservableCollection<TopCustomerDto> TopCustomers { get; } = new();

    public ManagerDashboardViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var today = _calendar.GetCurrentPersianDate();
            TodayText = today;
            var d = await _mediator.Send(new GetManagerDashboardQuery(today));
            TodaySales = d.TodaySales;
            MonthSales = d.MonthSales;
            MonthProfit = d.MonthProfit;
            MonthMarginPercent = d.MonthMarginPercent;
            ReceivablesTotal = d.ReceivablesTotal;
            PayablesTotal = d.PayablesTotal;
            ChequesInProcess = d.ChequesInProcess;
            TopCustomers.Clear();
            foreach (var c in d.TopCustomers) TopCustomers.Add(c);
        }, "در حال بارگذاری داشبورد مدیریتی...");
    }

    [RelayCommand] private Task RefreshAsync() => LoadAsync();
}
