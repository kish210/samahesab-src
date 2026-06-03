using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Queries;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Reports;

public partial class FinancialReportsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IAccountRepository _accountRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private string _selectedReportType = "تراز آزمایشی";
    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private int? _selectedAccountId;

    [ObservableProperty] private bool _isTrial = true;
    [ObservableProperty] private bool _isLedger;
    [ObservableProperty] private bool _isProfitLoss;
    [ObservableProperty] private bool _isBalanceSheet;
    [ObservableProperty] private bool _showDrCrTotals = true;

    [ObservableProperty] private decimal _totalDebit;
    [ObservableProperty] private decimal _totalCredit;
    [ObservableProperty] private decimal _revenueTotal;
    [ObservableProperty] private decimal _expenseTotal;
    [ObservableProperty] private decimal _netProfit;
    [ObservableProperty] private decimal _totalAssets;
    [ObservableProperty] private decimal _totalLiabEquity;
    [ObservableProperty] private bool _balanceOk;

    public List<string> ReportTypes { get; } = new() { "تراز آزمایشی", "دفتر کل / معین", "سود و زیان", "ترازنامه" };
    public ObservableCollection<TrialBalanceRow> TrialRows { get; } = new();
    public ObservableCollection<LedgerRow> LedgerRows { get; } = new();
    public ObservableCollection<PlDisplayRow> PlRows { get; } = new();
    public ObservableCollection<BalanceDisplayRow> BalanceRows { get; } = new();
    public List<AccountPick> Accounts { get; private set; } = new();

    public FinancialReportsViewModel(IMediator mediator, IAccountRepository accountRepo,
        ICurrentUserService currentUser, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _accountRepo = accountRepo;
        _currentUser = currentUser; _calendar = calendar;
    }

    public override async Task LoadAsync()
    {
        var cal = new System.Globalization.PersianCalendar();
        var now = DateTime.Now;
        FromDate = $"{cal.GetYear(now)}/{cal.GetMonth(now):D2}/01";
        ToDate = _calendar.GetCurrentPersianDate();

        var accs = await _accountRepo.GetByCompanyAsync(_currentUser.CompanyId ?? 1);
        Accounts = accs.Where(a => a.IsLeaf).OrderBy(a => a.Code)
            .Select(a => new AccountPick(a.Id, $"{a.Code} - {a.Name}")).ToList();
        OnPropertyChanged(nameof(Accounts));
        await RunAsync();
    }

    partial void OnSelectedReportTypeChanged(string value)
    {
        IsTrial = value == "تراز آزمایشی";
        IsLedger = value == "دفتر کل / معین";
        IsProfitLoss = value == "سود و زیان";
        IsBalanceSheet = value == "ترازنامه";
        ShowDrCrTotals = IsTrial || IsLedger;
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        await ExecuteAsync(async () =>
        {
            if (IsTrial)
            {
                TrialRows.Clear();
                var rows = await _mediator.Send(new GetTrialBalanceQuery(FromDate, ToDate));
                foreach (var r in rows) TrialRows.Add(r);
                TotalDebit = rows.Sum(r => r.Debit);
                TotalCredit = rows.Sum(r => r.Credit);
            }
            else if (IsLedger)
            {
                LedgerRows.Clear();
                var rows = await _mediator.Send(new GetGeneralLedgerQuery(FromDate, ToDate, SelectedAccountId));
                foreach (var r in rows) LedgerRows.Add(r);
                TotalDebit = rows.Sum(r => r.Debit);
                TotalCredit = rows.Sum(r => r.Credit);
            }
            else if (IsProfitLoss)
            {
                PlRows.Clear();
                var pl = await _mediator.Send(new GetProfitLossQuery(FromDate, ToDate));
                foreach (var l in pl.Revenues) PlRows.Add(new PlDisplayRow("درآمد", l.Code, l.Name, l.Amount));
                foreach (var l in pl.Expenses) PlRows.Add(new PlDisplayRow("هزینه", l.Code, l.Name, l.Amount));
                RevenueTotal = pl.Revenue; ExpenseTotal = pl.Expense; NetProfit = pl.NetProfit;
            }
            else // Balance sheet
            {
                BalanceRows.Clear();
                var bs = await _mediator.Send(new GetBalanceSheetQuery(FromDate, ToDate));
                foreach (var l in bs.Assets) BalanceRows.Add(new BalanceDisplayRow("دارایی", l.Code, l.Name, l.Amount));
                foreach (var l in bs.Liabilities) BalanceRows.Add(new BalanceDisplayRow("بدهی", l.Code, l.Name, l.Amount));
                foreach (var l in bs.Equity) BalanceRows.Add(new BalanceDisplayRow("حقوق صاحبان سهام", l.Code, l.Name, l.Amount));
                TotalAssets = bs.TotalAssets;
                TotalLiabEquity = bs.TotalLiabilities + bs.TotalEquity;
                NetProfit = bs.NetProfit;
                BalanceOk = bs.IsBalanced;
            }
        }, "در حال تهیه گزارش...");
    }

    [RelayCommand] private async Task PrintAsync() => await _dialogService.ShowInfoAsync("در حال آماده‌سازی چاپ گزارش...");
}

public record AccountPick(int Id, string Display);
public record PlDisplayRow(string Kind, string Code, string Name, decimal Amount);
public record BalanceDisplayRow(string Section, string Code, string Name, decimal Amount);
