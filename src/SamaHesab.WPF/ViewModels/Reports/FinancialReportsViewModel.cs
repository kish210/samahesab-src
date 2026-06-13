using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Accounting.Dimensions;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Application.Reports.Queries;
using SamaHesab.Application.Settings.Commands;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Reports;

public partial class FinancialReportsViewModel : BaseViewModel, SamaHesab.WPF.Services.INavigationAware
{
    private readonly IMediator _mediator;
    private readonly IAccountRepository _accountRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private string _selectedReportType = "تراز آزمایشی";
    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private int? _selectedAccountId;
    [ObservableProperty] private int? _selectedBranchId;   // null = همهٔ شعب

    public ObservableCollection<BranchPick> Branches { get; } = new();
    [ObservableProperty] private int? _selectedCostCenterId;
    [ObservableProperty] private int? _selectedProjectId;

    [ObservableProperty] private bool _isTrial = true;
    [ObservableProperty] private bool _isLedger;
    [ObservableProperty] private bool _isProfitLoss;
    [ObservableProperty] private bool _isBalanceSheet;
    [ObservableProperty] private bool _isCashFlow;
    [ObservableProperty] private bool _showDrCrTotals = true;

    // صورت جریان وجوه نقد
    [ObservableProperty] private decimal _cfOperating;
    [ObservableProperty] private decimal _cfInvesting;
    [ObservableProperty] private decimal _cfFinancing;
    [ObservableProperty] private decimal _cfNetChange;
    [ObservableProperty] private decimal _cfOpening;
    [ObservableProperty] private decimal _cfClosing;

    [ObservableProperty] private decimal _totalDebit;
    [ObservableProperty] private decimal _totalCredit;
    [ObservableProperty] private decimal _revenueTotal;
    [ObservableProperty] private decimal _expenseTotal;
    [ObservableProperty] private decimal _netProfit;
    [ObservableProperty] private decimal _totalAssets;
    [ObservableProperty] private decimal _totalLiabEquity;
    [ObservableProperty] private bool _balanceOk;

    public List<string> ReportTypes { get; } = new() { "تراز آزمایشی", "دفتر کل / معین", "سود و زیان", "ترازنامه", "صورت جریان وجوه" };
    public ObservableCollection<TrialBalanceRow> TrialRows { get; } = new();
    public ObservableCollection<LedgerRow> LedgerRows { get; } = new();
    public ObservableCollection<PlDisplayRow> PlRows { get; } = new();
    public ObservableCollection<BalanceDisplayRow> BalanceRows { get; } = new();
    public List<AccountPick> Accounts { get; private set; } = new();
    public List<CostCenterDto> CostCenters { get; private set; } = new();
    public List<ProjectDto> Projects { get; private set; } = new();

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

        // ابعاد تحلیلی برای فیلتر گزارش (هستهٔ ERP)
        CostCenters = await _mediator.Send(new GetCostCentersQuery(ActiveOnly: true));
        OnPropertyChanged(nameof(CostCenters));
        Projects = await _mediator.Send(new GetProjectsQuery(ActiveOnly: true));
        OnPropertyChanged(nameof(Projects));

        Branches.Clear();
        Branches.Add(new BranchPick(null, "همهٔ شعب"));
        foreach (var b in await _mediator.Send(new GetBranchesQuery()))
            Branches.Add(new BranchPick(b.Id, $"{b.Code} - {b.Name}"));
        SelectedBranchId = null;

        await RunAsync();
    }

    /// <summary>ورود با پارامترِ شناسهٔ حساب → بازکردنِ «دفتر کل» همان حساب (از نمودار حساب‌ها).</summary>
    public async Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is int accountId && accountId > 0)
        {
            SelectedReportType = "دفتر کل / معین";   // → IsLedger=true
            SelectedAccountId = accountId;
            await RunAsync();
        }
    }

    partial void OnSelectedReportTypeChanged(string value)
    {
        IsTrial = value == "تراز آزمایشی";
        IsLedger = value == "دفتر کل / معین";
        IsProfitLoss = value == "سود و زیان";
        IsBalanceSheet = value == "ترازنامه";
        IsCashFlow = value == "صورت جریان وجوه";
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
                var rows = await _mediator.Send(new GetTrialBalanceQuery(FromDate, ToDate, SelectedCostCenterId, SelectedProjectId, SelectedBranchId));
                foreach (var r in rows) TrialRows.Add(r);
                TotalDebit = rows.Sum(r => r.Debit);
                TotalCredit = rows.Sum(r => r.Credit);
            }
            else if (IsLedger)
            {
                LedgerRows.Clear();
                var rows = await _mediator.Send(new GetGeneralLedgerQuery(FromDate, ToDate, SelectedAccountId, SelectedCostCenterId, SelectedProjectId, SelectedBranchId));
                foreach (var r in rows) LedgerRows.Add(r);
                TotalDebit = rows.Sum(r => r.Debit);
                TotalCredit = rows.Sum(r => r.Credit);
            }
            else if (IsProfitLoss)
            {
                PlRows.Clear();
                var pl = await _mediator.Send(new GetProfitLossQuery(FromDate, ToDate, SelectedBranchId));
                foreach (var l in pl.Revenues) PlRows.Add(new PlDisplayRow("درآمد", l.Code, l.Name, l.Amount));
                foreach (var l in pl.Expenses) PlRows.Add(new PlDisplayRow("هزینه", l.Code, l.Name, l.Amount));
                RevenueTotal = pl.Revenue; ExpenseTotal = pl.Expense; NetProfit = pl.NetProfit;
            }
            else if (IsBalanceSheet)
            {
                BalanceRows.Clear();
                var bs = await _mediator.Send(new GetBalanceSheetQuery(FromDate, ToDate, SelectedBranchId));
                foreach (var l in bs.Assets) BalanceRows.Add(new BalanceDisplayRow("دارایی", l.Code, l.Name, l.Amount));
                foreach (var l in bs.Liabilities) BalanceRows.Add(new BalanceDisplayRow("بدهی", l.Code, l.Name, l.Amount));
                foreach (var l in bs.Equity) BalanceRows.Add(new BalanceDisplayRow("حقوق صاحبان سهام", l.Code, l.Name, l.Amount));
                TotalAssets = bs.TotalAssets;
                TotalLiabEquity = bs.TotalLiabilities + bs.TotalEquity;
                NetProfit = bs.NetProfit;
                BalanceOk = bs.IsBalanced;
            }
            else // Cash flow
            {
                var cf = await _mediator.Send(new GetCashFlowQuery(FromDate, ToDate, SelectedBranchId));
                CfOperating = cf.Operating; CfInvesting = cf.Investing; CfFinancing = cf.Financing;
                CfNetChange = cf.NetChange; CfOpening = cf.OpeningCash; CfClosing = cf.ClosingCash;
            }
        }, "در حال تهیه گزارش...");
    }

    /// <summary>جدول گزارشِ جاری را برای خروجی‌گیری می‌سازد (ارقام با جداکنندهٔ هزارگان).</summary>
    private ReportTable BuildTable()
    {
        string N(decimal d) => d.ToString("N0");
        if (IsTrial)
            return new ReportTable("تراز آزمایشی",
                new[] { "کد حساب", "نام حساب", "بدهکار", "بستانکار", "مانده" },
                TrialRows.Select(r => new[] { r.Code, r.Name, N(r.Debit), N(r.Credit), N(r.Balance) }).ToList());
        if (IsLedger)
            return new ReportTable("دفتر کل / معین",
                new[] { "تاریخ", "شماره سند", "کد حساب", "نام حساب", "شرح", "بدهکار", "بستانکار", "مانده" },
                LedgerRows.Select(r => new[] { r.Date, r.VoucherNumber, r.Code, r.Name, r.Description ?? "",
                    N(r.Debit), N(r.Credit), N(r.Balance) }).ToList());
        if (IsProfitLoss)
            return new ReportTable("سود و زیان",
                new[] { "نوع", "کد حساب", "نام حساب", "مبلغ" },
                PlRows.Select(r => new[] { r.Kind, r.Code, r.Name, N(r.Amount) }).ToList());
        if (IsCashFlow)
            return new ReportTable("صورت جریان وجوه نقد",
                new[] { "بخش", "مبلغ" },
                new List<string[]>
                {
                    new[] { "ماندهٔ ابتدای دوره", N(CfOpening) },
                    new[] { "جریان نقد عملیاتی", N(CfOperating) },
                    new[] { "جریان نقد سرمایه‌گذاری", N(CfInvesting) },
                    new[] { "جریان نقد تأمین مالی", N(CfFinancing) },
                    new[] { "خالص تغییر وجه نقد", N(CfNetChange) },
                    new[] { "ماندهٔ پایان دوره", N(CfClosing) },
                });
        return new ReportTable("ترازنامه",
            new[] { "بخش", "کد حساب", "نام حساب", "مبلغ" },
            BalanceRows.Select(r => new[] { r.Section, r.Code, r.Name, N(r.Amount) }).ToList());
    }

    private bool HasRows() =>
        (IsTrial && TrialRows.Count > 0) || (IsLedger && LedgerRows.Count > 0) ||
        (IsProfitLoss && PlRows.Count > 0) || (IsBalanceSheet && BalanceRows.Count > 0) ||
        IsCashFlow;

    private string SaveTo(string ext, string content)
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
        System.IO.Directory.CreateDirectory(dir);
        var name = $"{SelectedReportType}_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}";
        var path = System.IO.Path.Combine(dir, name);
        // UTF-8 با BOM تا اکسل/مرورگر فارسی را درست نشان دهد
        System.IO.File.WriteAllText(path, content, new System.Text.UTF8Encoding(true));
        return path;
    }

    private void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });

    /// <summary>خروجی CSV (باز در اکسل).</summary>
    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (!HasRows()) { await _dialogService.ShowWarningAsync("ابتدا گزارش را تهیه کنید."); return; }
        try
        {
            var path = SaveTo("csv", ReportExporter.ToCsv(BuildTable()));
            OpenFile(path);
            await _dialogService.ShowSuccessAsync($"خروجی اکسل ذخیره شد:\n{path}");
        }
        catch (Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    /// <summary>خروجی HTML راست‌چین — برای چاپ یا تبدیل به PDF در مرورگر.</summary>
    [RelayCommand]
    private async Task PrintAsync()
    {
        if (!HasRows()) { await _dialogService.ShowWarningAsync("ابتدا گزارش را تهیه کنید."); return; }
        try
        {
            var path = SaveTo("html", ReportExporter.ToHtml(BuildTable()));
            OpenFile(path);   // مرورگر باز می‌شود؛ کاربر با Ctrl+P → ذخیره به‌صورت PDF
            await _dialogService.ShowInfoAsync("گزارش در مرورگر باز شد. برای PDF از «چاپ ← ذخیره به‌صورت PDF» استفاده کنید.");
        }
        catch (Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }
}

public record AccountPick(int Id, string Display);
public record BranchPick(int? Id, string Display);
public record PlDisplayRow(string Kind, string Code, string Name, decimal Amount);
public record BalanceDisplayRow(string Section, string Code, string Name, decimal Amount);
