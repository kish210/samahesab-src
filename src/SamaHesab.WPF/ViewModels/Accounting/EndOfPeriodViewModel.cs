using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Accounting;

/// <summary>
/// عملیات پایان دوره (R6): سند برگشتی + بستن سال مالی.
/// بک‌اند آماده است؛ این VM فقط فرمان‌ها را با IMediator صدا می‌زند.
/// </summary>
public partial class EndOfPeriodViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;

    // ── سند برگشتی ──
    [ObservableProperty] private int _reverseVoucherId;
    [ObservableProperty] private string _reverseDate = string.Empty;
    [ObservableProperty] private string _reverseDescription = string.Empty;
    [ObservableProperty] private string? _reverseResult;

    // ── بستن سال مالی ──
    [ObservableProperty] private int _fiscalYearId = 1;
    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private string _closingDate = string.Empty;
    [ObservableProperty] private bool _generateOpening;
    [ObservableProperty] private int _nextFiscalYearId;
    [ObservableProperty] private string _openingDate = string.Empty;

    // نتیجهٔ بستن دوره (برای نمایش)
    [ObservableProperty] private bool _hasCloseResult;
    [ObservableProperty] private decimal _resultRevenue;
    [ObservableProperty] private decimal _resultExpense;
    [ObservableProperty] private decimal _resultNetProfit;
    [ObservableProperty] private int _resultClosingVoucherId;
    [ObservableProperty] private int? _resultOpeningVoucherId;
    [ObservableProperty] private string? _closeResultMessage;

    public EndOfPeriodViewModel(
        IMediator mediator,
        ICurrentUserService currentUser,
        IPersianCalendarService calendar,
        IDialogService dialogService,
        INavigationService navigationService) : base(dialogService, navigationService)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _calendar = calendar;
    }

    public override Task LoadAsync()
    {
        var today = _calendar.GetCurrentPersianDate();
        var cal = new System.Globalization.PersianCalendar();
        var year = cal.GetYear(DateTime.Now);

        ReverseDate = today;
        FromDate = $"{year}/01/01";
        ToDate = $"{year}/12/29";
        ClosingDate = $"{year}/12/29";
        OpeningDate = $"{year + 1}/01/01";
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ReverseAsync()
    {
        if (ReverseVoucherId <= 0)
        { await _dialogService.ShowWarningAsync("شمارهٔ شناسهٔ سند را وارد کنید."); return; }

        if (!await _dialogService.ConfirmAsync(
                $"برای سند شناسهٔ {ReverseVoucherId} یک سند برگشتی (معکوس) صادر شود؟"))
            return;

        await ExecuteAsync(async () =>
        {
            var result = await _mediator.Send(new ReverseVoucherCommand(
                ReverseVoucherId, ReverseDate,
                string.IsNullOrWhiteSpace(ReverseDescription) ? null : ReverseDescription));

            if (result.Succeeded)
            {
                ReverseResult = $"سند برگشتی با شناسهٔ {result.Value} صادر شد.";
                await _dialogService.ShowSuccessAsync(ReverseResult);
            }
            else
            {
                ReverseResult = null;
                await _dialogService.ShowErrorAsync(
                    string.IsNullOrEmpty(result.ErrorMessage) ? "ثبت سند برگشتی ناموفق بود." : result.ErrorMessage);
            }
        }, "در حال صدور سند برگشتی...");
    }

    [RelayCommand]
    private async Task CloseFiscalYearAsync()
    {
        if (!await _dialogService.ConfirmAsync(
                "سند اختتامیه صادر و حساب‌های سود و زیان بسته شوند؟ این عملیات بازگشت‌ناپذیر است."))
            return;

        await ExecuteAsync(async () =>
        {
            var result = await _mediator.Send(new CloseFiscalYearCommand(
                FiscalYearId: FiscalYearId,
                BranchId: _currentUser.BranchId ?? 1,
                FromDate: FromDate,
                ToDate: ToDate,
                ClosingDate: ClosingDate,
                GenerateOpening: GenerateOpening,
                NextFiscalYearId: NextFiscalYearId,
                OpeningDate: string.IsNullOrWhiteSpace(OpeningDate) ? null : OpeningDate));

            if (result.Succeeded)
            {
                var r = result.Value!;
                HasCloseResult = true;
                ResultRevenue = r.Revenue;
                ResultExpense = r.Expense;
                ResultNetProfit = r.NetProfit;
                ResultClosingVoucherId = r.ClosingVoucherId;
                ResultOpeningVoucherId = r.OpeningVoucherId;
                CloseResultMessage = r.NetProfit >= 0
                    ? $"سود دوره: {r.NetProfit:N0} ریال"
                    : $"زیان دوره: {-r.NetProfit:N0} ریال";
                await _dialogService.ShowSuccessAsync(
                    $"سند اختتامیه (شناسه {r.ClosingVoucherId}) صادر شد." +
                    (r.OpeningVoucherId is int oid ? $" سند افتتاحیه: {oid}." : ""));
            }
            else
            {
                HasCloseResult = false;
                await _dialogService.ShowErrorAsync(
                    string.IsNullOrEmpty(result.ErrorMessage) ? "بستن سال مالی ناموفق بود." : result.ErrorMessage);
            }
        }, "در حال بستن سال مالی...");
    }
}
