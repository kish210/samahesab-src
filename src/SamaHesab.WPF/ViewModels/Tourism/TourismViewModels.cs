using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Dimensions;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Tourism;
using SamaHesab.Application.Tourism.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Tourism;

/// <summary>یک گزینهٔ حساب برای دراپ‌داون‌های تنظیمات (کد — نام).</summary>
public record AccountPick(int Id, string Display);

// ─── ودیعهٔ تأمین‌کنندگان (TUR-C2-4) ──────────────────────────────────────────
public partial class TourismDepositsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;
    private readonly ICurrentUserService _user;

    [ObservableProperty] private bool _onlyLow;
    [ObservableProperty] private int _selectedSupplierId;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private string _paymentMethod = "بانک";
    [ObservableProperty] private string _date = string.Empty;
    [ObservableProperty] private int _selectedFiscalYearId;
    [ObservableProperty] private decimal _totalRemaining;

    public ObservableCollection<SupplierDepositBalanceDto> Balances { get; } = new();
    public ObservableCollection<SupplierRowDto> Suppliers { get; } = new();
    public ObservableCollection<FiscalYearDto> FiscalYears { get; } = new();
    public string[] PaymentMethods { get; } = { "بانک", "نقد" };

    public TourismDepositsViewModel(IMediator mediator, IPersianCalendarService calendar,
        ICurrentUserService user, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; _user = user; }

    public override async Task LoadAsync()
    {
        Date = _calendar.GetCurrentPersianDate();
        await ExecuteAsync(async () =>
        {
            Suppliers.Clear();
            foreach (var s in await _mediator.Send(new GetSuppliersQuery())) Suppliers.Add(s);
            FiscalYears.Clear();
            foreach (var f in await _mediator.Send(new GetFiscalYearsQuery())) FiscalYears.Add(f);
            if (SelectedFiscalYearId == 0)
                SelectedFiscalYearId = FiscalYears.FirstOrDefault(f => f.IsActive)?.Id
                                       ?? FiscalYears.FirstOrDefault()?.Id ?? 0;
            await ReloadBalancesAsync();
        }, "در حال بارگذاری...");
    }

    [RelayCommand] private async Task RefreshAsync() => await ReloadBalancesAsync();
    partial void OnOnlyLowChanged(bool value) => _ = ReloadBalancesAsync();

    private async Task ReloadBalancesAsync()
    {
        Balances.Clear();
        foreach (var b in await _mediator.Send(new GetSupplierDepositBalancesQuery(OnlyLow))) Balances.Add(b);
        TotalRemaining = Balances.Sum(b => b.Remaining);
    }

    /// <summary>شارژِ ودیعهٔ تأمین‌کننده (سندِ Dr ودیعه / Cr بانک‌یا‌نقد).</summary>
    [RelayCommand]
    private async Task TopUpAsync()
    {
        if (SelectedSupplierId <= 0) { await _dialogService.ShowWarningAsync("تأمین‌کننده را انتخاب کنید."); return; }
        if (Amount <= 0) { await _dialogService.ShowWarningAsync("مبلغِ شارژ باید بزرگ‌تر از صفر باشد."); return; }
        if (SelectedFiscalYearId <= 0) { await _dialogService.ShowWarningAsync("سالِ مالی را انتخاب کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new TopUpSupplierDepositCommand(
                _user.BranchId ?? 1, SelectedFiscalYearId, Date, SelectedSupplierId, Amount, PaymentMethod));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync($"شارژِ ودیعه ثبت و سندِ حسابداری صادر شد (مبلغ {Amount:N0} ریال).");
            Amount = 0;
            await ReloadBalancesAsync();
        }, "در حال ثبتِ شارژ...");
    }
}

// ─── پورسانتِ ماهانهٔ فروشندگان (TUR-C2-4) ────────────────────────────────────
public partial class TourismCommissionsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private string _periodYearMonth = string.Empty;
    [ObservableProperty] private decimal _totalCommission;

    public ObservableCollection<EmployeeCommissionDto> Rows { get; } = new();

    public TourismCommissionsViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(PeriodYearMonth))
        {
            var now = System.DateTime.Now;
            PeriodYearMonth = $"{_calendar.GetPersianYear(now)}/{_calendar.GetPersianMonth(now):00}";
        }
        await LoadRowsAsync();
    }

    [RelayCommand]
    private async Task LoadRowsAsync()
    {
        await ExecuteAsync(async () =>
        {
            Rows.Clear();
            foreach (var r in await _mediator.Send(new GetMonthlyCommissionByEmployeeQuery(PeriodYearMonth))) Rows.Add(r);
            TotalCommission = Rows.Sum(r => r.Commission);
        }, "در حال محاسبهٔ پورسانت...");
    }
}

// ─── تنظیماتِ گردشگری (TUR-C2-4) ──────────────────────────────────────────────
public partial class TourismSettingsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    [ObservableProperty] private int? _cashAccountId;
    [ObservableProperty] private int? _bankAccountId;
    [ObservableProperty] private int? _receivableAccountId;
    [ObservableProperty] private int? _revenueAccountId;
    [ObservableProperty] private int? _cogsAccountId;
    [ObservableProperty] private int? _supplierDepositAccountId;
    [ObservableProperty] private int? _salesDiscountAccountId;
    [ObservableProperty] private int? _depositDifferenceAccountId;
    [ObservableProperty] private int? _commissionExpenseAccountId;
    [ObservableProperty] private int? _salespersonPayableAccountId;
    [ObservableProperty] private bool _saleBaseAfterDiscountDefault = true;
    [ObservableProperty] private decimal _lowDepositThreshold;
    [ObservableProperty] private bool _postPerSale = true;
    [ObservableProperty] private bool _commissionThroughPayroll = true;

    public ObservableCollection<AccountPick> Accounts { get; } = new();

    public TourismSettingsViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            Accounts.Clear();
            foreach (var a in await _mediator.Send(new GetAccountsQuery(LeafOnly: true)))
                Accounts.Add(new AccountPick(a.Id, $"{a.Code} — {a.Name}"));

            var s = await _mediator.Send(new GetTourismSettingsQuery());
            CashAccountId = s.CashAccountId; BankAccountId = s.BankAccountId;
            ReceivableAccountId = s.ReceivableAccountId; RevenueAccountId = s.RevenueAccountId;
            CogsAccountId = s.CogsAccountId; SupplierDepositAccountId = s.SupplierDepositAccountId;
            SalesDiscountAccountId = s.SalesDiscountAccountId; DepositDifferenceAccountId = s.DepositDifferenceAccountId;
            CommissionExpenseAccountId = s.CommissionExpenseAccountId; SalespersonPayableAccountId = s.SalespersonPayableAccountId;
            SaleBaseAfterDiscountDefault = s.SaleBaseAfterDiscountDefault; LowDepositThreshold = s.LowDepositThreshold;
            PostPerSale = s.PostPerSale; CommissionThroughPayroll = s.CommissionThroughPayroll;
        }, "در حال بارگذاریِ تنظیمات...");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await ExecuteAsync(async () =>
        {
            var dto = new TourismSettingsDto(
                CashAccountId, ReceivableAccountId, RevenueAccountId, CogsAccountId,
                SupplierDepositAccountId, SalesDiscountAccountId, DepositDifferenceAccountId,
                CommissionExpenseAccountId, SalespersonPayableAccountId, BankAccountId,
                SaleBaseAfterDiscountDefault, LowDepositThreshold, PostPerSale, CommissionThroughPayroll);
            var res = await _mediator.Send(new SaveTourismSettingsCommand(dto));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync("تنظیماتِ گردشگری ذخیره شد.");
        }, "در حال ذخیره...");
    }
}
