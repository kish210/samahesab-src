using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Accounting;

/// <summary>
/// مغایرت‌گیری بانکی (R4 / #۱۹): دفتر بانک سیستم را با صورت‌حساب واردشدهٔ بانک
/// (CSV: تاریخ,مبلغ[,شرح]) به‌صورت خودکار تطبیق می‌دهد و نامنطبق‌های هر طرف را نشان می‌دهد.
/// از موتورهای خالص BankStatementParser + BankReconciliation استفاده می‌کند.
/// </summary>
public partial class BankReconciliationViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ApiClient _api;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;

    private List<BankLedgerLineDto> _ledger = new();
    private List<int> _matchedItemIds = new();

    public ObservableCollection<BankAccountOption> BankAccounts { get; } = new();
    public ObservableCollection<ReconMatchRow> Matched { get; } = new();
    public ObservableCollection<BankLedgerLineDto> UnmatchedLedger { get; } = new();
    public ObservableCollection<StatementLine> UnmatchedStatement { get; } = new();

    [ObservableProperty] private int _selectedBankAccountId;
    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private string _statementText = string.Empty;

    [ObservableProperty] private int _ledgerCount;
    [ObservableProperty] private int _matchedCount;
    [ObservableProperty] private int _unmatchedLedgerCount;
    [ObservableProperty] private int _unmatchedStatementCount;
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private string _lastReconInfo = string.Empty;

    public BankReconciliationViewModel(
        IMediator mediator,
        ApiClient api,
        ICurrentUserService currentUser,
        IPersianCalendarService calendar,
        IDialogService dialogService,
        INavigationService navigationService) : base(dialogService, navigationService)
    {
        _mediator = mediator;
        _api = api;
        _currentUser = currentUser;
        _calendar = calendar;
    }

    public override async Task LoadAsync()
    {
        var cal = new System.Globalization.PersianCalendar();
        var year = cal.GetYear(DateTime.Now);
        FromDate = $"{year}/01/01";
        ToDate = _calendar.GetCurrentPersianDate();

        await ExecuteAsync(async () =>
        {
            BankAccounts.Clear();
            // 🏛️ کلاینت→API، دسکتاپ→Application
            if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
                foreach (var b in await _api.GetBankAccountsAsync(activeOnly: true))
                    BankAccounts.Add(new BankAccountOption(b.Id, $"{b.BankName} — {b.AccountNumber}"));
            else
                foreach (var b in await _mediator.Send(new SamaHesab.Application.Accounting.Queries.GetBankAccountsQuery(ActiveOnly: true)))
                    BankAccounts.Add(new BankAccountOption(b.Id, $"{b.BankName} — {b.AccountNumber}"));
            SelectedBankAccountId = BankAccounts.FirstOrDefault()?.Id ?? 0;
        }, "در حال بارگذاری حساب‌های بانکی...");
    }

    /// <summary>بارگذاری دفتر بانک از روی اسناد ثبت‌شده در بازهٔ انتخابی.</summary>
    [RelayCommand]
    private async Task LoadLedgerAsync()
    {
        if (SelectedBankAccountId <= 0)
        { await _dialogService.ShowWarningAsync("یک حساب بانکی را انتخاب کنید."); return; }

        await ExecuteAsync(async () =>
        {
            var result = await _mediator.Send(new GetBankLedgerQuery(SelectedBankAccountId, FromDate, ToDate));

            // ردیف‌هایی که قبلاً تطبیق شده‌اند را کنار بگذار (ماندگاریِ سبک).
            var state = BankReconciliationStore.Get(SelectedBankAccountId);
            var already = new HashSet<int>(state.ReconciledItemIds);
            var skipped = result.Lines.Count(l => already.Contains(l.VoucherItemId));
            _ledger = result.Lines.Where(l => !already.Contains(l.VoucherItemId)).ToList();

            LedgerCount = _ledger.Count;
            LastReconInfo = string.IsNullOrEmpty(state.LastDate)
                ? "بدون تطبیق قبلی"
                : $"آخرین تطبیق: {state.LastDate} — {state.ReconciledItemIds.Count} ردیف تطبیق‌شدهٔ ماندگار";
            // نتیجهٔ قبلی پاک شود تا کاربر دوباره تطبیق بزند
            ResetResult();
            await _dialogService.ShowInfoAsync(
                $"{LedgerCount} ردیف باز برای «{result.BankName}» بارگذاری شد" +
                (skipped > 0 ? $" ({skipped} ردیف قبلاً تطبیق‌شده کنار گذاشته شد)" : "") +
                ". اکنون صورت‌حساب را وارد و «تطبیق خودکار» را بزنید.");
        }, "در حال بارگذاری دفتر بانک...");
    }

    /// <summary>تطبیق خودکار دفتر با صورت‌حساب واردشده.</summary>
    [RelayCommand]
    private async Task ReconcileAsync()
    {
        if (_ledger.Count == 0)
        { await _dialogService.ShowWarningAsync("ابتدا دفتر بانک را بارگذاری کنید."); return; }

        var statement = BankStatementParser.Parse(StatementText);
        if (statement.Count == 0)
        { await _dialogService.ShowWarningAsync("صورت‌حساب معتبری وارد نشده است. هر خط: تاریخ,مبلغ[,شرح]"); return; }

        var ledgerLines = _ledger.Select(l => new LedgerLine(l.VoucherItemId, l.Date, l.Amount));
        var recon = BankReconciliation.AutoMatch(ledgerLines, statement);

        var byId = _ledger.ToDictionary(l => l.VoucherItemId);

        Matched.Clear();
        _matchedItemIds = recon.Matched.Select(m => m.Ledger.VoucherItemId).ToList();
        foreach (var m in recon.Matched)
        {
            var desc = byId.TryGetValue(m.Ledger.VoucherItemId, out var dto) ? dto.Description : "";
            Matched.Add(new ReconMatchRow(m.Ledger.Date, m.Ledger.Amount, desc, m.Statement.Reference ?? ""));
        }

        UnmatchedLedger.Clear();
        foreach (var l in recon.UnmatchedLedger)
            UnmatchedLedger.Add(byId.TryGetValue(l.VoucherItemId, out var dto)
                ? dto : new BankLedgerLineDto(l.VoucherItemId, l.Date, l.Amount, ""));

        UnmatchedStatement.Clear();
        foreach (var s in recon.UnmatchedStatement)
            UnmatchedStatement.Add(s);

        MatchedCount = Matched.Count;
        UnmatchedLedgerCount = UnmatchedLedger.Count;
        UnmatchedStatementCount = UnmatchedStatement.Count;
        HasResult = true;
    }

    /// <summary>ثبت ماندگارِ ردیف‌های تطبیق‌شده تا در بارگذاری‌های بعدی تکرار نشوند.</summary>
    [RelayCommand]
    private async Task CommitReconcileAsync()
    {
        if (_matchedItemIds.Count == 0)
        { await _dialogService.ShowWarningAsync("ردیف تطبیق‌شده‌ای برای ثبت وجود ندارد."); return; }

        if (!await _dialogService.ConfirmAsync(
                $"{_matchedItemIds.Count} ردیف تطبیق‌شده به‌صورت ماندگار ثبت شوند؟ این ردیف‌ها در بارگذاری‌های بعدی نمایش داده نمی‌شوند."))
            return;

        var today = _calendar.GetCurrentPersianDate();
        BankReconciliationStore.AddReconciled(SelectedBankAccountId, _matchedItemIds, today);
        await _dialogService.ShowSuccessAsync($"{_matchedItemIds.Count} ردیف تطبیق ثبت شد.");
        await LoadLedgerAsync();   // بازخوانی → ردیف‌های ثبت‌شده کنار می‌روند
    }

    private void ResetResult()
    {
        Matched.Clear(); UnmatchedLedger.Clear(); UnmatchedStatement.Clear();
        _matchedItemIds = new();
        MatchedCount = UnmatchedLedgerCount = UnmatchedStatementCount = 0;
        HasResult = false;
    }
}

public record BankAccountOption(int Id, string Display);
public record ReconMatchRow(string Date, decimal Amount, string Description, string Reference);
