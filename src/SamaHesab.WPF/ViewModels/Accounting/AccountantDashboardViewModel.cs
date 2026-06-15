using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.BI.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Accounting;

/// <summary>
/// F9-4 — داشبوردِ حسابدار/مدیریتیِ مالی: KPIهای کلیدیِ حسابداری/خزانه روی
/// `GetAccountantDashboardQuery`ِ آماده. کاشی‌ها کلیک‌پذیرند (ناوبریِ سریع).
/// </summary>
public partial class AccountantDashboardViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private string _todayText = string.Empty;
    [ObservableProperty] private int _draftVouchers;
    [ObservableProperty] private decimal _receivablesTotal;
    [ObservableProperty] private decimal _payablesTotal;
    [ObservableProperty] private int _chequesInProcess;
    [ObservableProperty] private int _chequesOverdue;
    [ObservableProperty] private int _chequesDueToday;

    public AccountantDashboardViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var today = _calendar.GetCurrentPersianDate();
            TodayText = today;
            var d = await _mediator.Send(new GetAccountantDashboardQuery(today));
            DraftVouchers = d.DraftVouchers;
            ReceivablesTotal = d.ReceivablesTotal;
            PayablesTotal = d.PayablesTotal;
            ChequesInProcess = d.ChequesInProcess;
            ChequesOverdue = d.ChequesOverdue;
            ChequesDueToday = d.ChequesDueToday;
        }, "در حال بارگذاری داشبورد حسابدار...");
    }

    [RelayCommand] private Task RefreshAsync() => LoadAsync();

    [RelayCommand] private void OpenVouchers() => _navigationService.NavigateTo("Vouchers");
    [RelayCommand] private void OpenReceivables() => _navigationService.NavigateTo("Receivables");
    [RelayCommand] private void OpenCheques() => _navigationService.NavigateTo("ChequeBoard");
}
