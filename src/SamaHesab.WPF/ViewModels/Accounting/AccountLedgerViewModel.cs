using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Reports;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Accounting;

/// <summary>
/// دفترِ معینِ یک حساب با ماندهٔ روان + **drill-down به سند** (رودمپ-گزارش‌ها، بک‌اندِ pc:
/// GetAccountLedgerQuery/AccountLedger). انتخابِ حساب + بازهٔ تاریخ → ردیف‌ها با ماندهٔ تجمعی؛
/// دوبارکلیک/دکمهٔ «بازکردنِ سند» → ناوبری به همان سند.
/// </summary>
public partial class AccountLedgerViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    public List<AccountPick> Accounts { get; private set; } = new();
    public ObservableCollection<AccountLedgerRow> Rows { get; } = new();

    [ObservableProperty] private int? _selectedAccountId;
    [ObservableProperty] private string _fromDate = "";
    [ObservableProperty] private string _toDate = "";
    [ObservableProperty] private AccountLedgerRow? _selectedRow;

    [ObservableProperty] private decimal _openingBalance;
    [ObservableProperty] private decimal _totalDebit;
    [ObservableProperty] private decimal _totalCredit;
    [ObservableProperty] private decimal _closingBalance;

    public AccountLedgerViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            Accounts = (await _mediator.Send(new GetAccountsQuery(LeafOnly: true)))
                .Select(a => new AccountPick(a.Id, $"{a.Code} - {a.Name}")).ToList();
            OnPropertyChanged(nameof(Accounts));
            var today = _calendar.GetCurrentPersianDate();
            ToDate = today;
            FromDate = (today.Length >= 4 ? today[..4] : "1405") + "/01/01";   // ابتدای سالِ جاری
            SelectedAccountId = Accounts.FirstOrDefault()?.Id;
        }, "در حال بارگذاری حساب‌ها...");
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (SelectedAccountId is not > 0) { await _dialogService.ShowWarningAsync("یک حساب را انتخاب کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new GetAccountLedgerQuery(SelectedAccountId.Value, FromDate, ToDate));
            Rows.Clear();
            foreach (var r in res.Rows) Rows.Add(r);
            OpeningBalance = res.OpeningBalance;
            TotalDebit = res.TotalDebit; TotalCredit = res.TotalCredit; ClosingBalance = res.ClosingBalance;
        }, "در حال تهیهٔ دفترِ معین...");
    }

    /// <summary>drill-down: بازکردنِ سندِ ردیفِ انتخاب‌شده.</summary>
    [RelayCommand]
    private void OpenVoucher()
    {
        if (SelectedRow is { VoucherId: > 0 } r) _navigationService.NavigateTo("VoucherEdit", r.VoucherId);
    }
}
