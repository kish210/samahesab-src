using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Dimensions;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Tourism;
using SamaHesab.Application.Tourism.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Tourism;

/// <summary>یک ردیفِ پیش‌نمایشِ سند (نقشِ فارسی + بدهکار/بستانکار + شرح).</summary>
public record TourismVoucherPreviewRow(string Role, decimal Debit, decimal Credit, string Description);

/// <summary>
/// M11 (فراخوانندهٔ C2) — تولیدِ سندِ حسابداریِ گردشگری از داده‌ی مالیِ رزرو.
/// با ورودِ مبالغ، پیش‌نمایشِ زندهٔ ردیف‌های متوازن از موتورِ خالصِ <see cref="TourismSettlement"/>
/// نمایش داده می‌شود؛ «ثبتِ سند» همان داده را به <see cref="GenerateTourismVoucherCommand"/> می‌دهد
/// (بک‌اندِ C1 — حساب‌ها را resolve و سندِ متوازن صادر می‌کند). هم‌الگوی ContractingStatementViewModel.
/// </summary>
public partial class TourismVoucherGenViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;
    private readonly ICurrentUserService _user;

    [ObservableProperty] private string _bookingRef = string.Empty;
    [ObservableProperty] private string _date = string.Empty;
    [ObservableProperty] private decimal _saleAmount;
    [ObservableProperty] private decimal _costAmount;
    [ObservableProperty] private decimal _commissionPercent;
    [ObservableProperty] private decimal _paidAmount;
    [ObservableProperty] private string _paymentMethod = "نقدی";
    [ObservableProperty] private int _selectedFiscalYearId;

    // پیش‌نمایشِ زنده
    [ObservableProperty] private decimal _commission;
    [ObservableProperty] private decimal _netProfit;
    [ObservableProperty] private decimal _totalDebit;
    [ObservableProperty] private decimal _totalCredit;
    [ObservableProperty] private bool _isBalanced;

    public ObservableCollection<TourismVoucherPreviewRow> Lines { get; } = new();
    public ObservableCollection<FiscalYearDto> FiscalYears { get; } = new();
    public string[] PaymentMethods { get; } = { "نقدی", "بانک" };

    public TourismVoucherGenViewModel(IMediator mediator, IPersianCalendarService calendar,
        ICurrentUserService user, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; _user = user; }

    public override async Task LoadAsync()
    {
        Date = _calendar.GetCurrentPersianDate();
        await ExecuteAsync(async () =>
        {
            FiscalYears.Clear();
            foreach (var f in await _mediator.Send(new GetFiscalYearsQuery())) FiscalYears.Add(f);
            if (SelectedFiscalYearId == 0)
                SelectedFiscalYearId = FiscalYears.FirstOrDefault(f => f.IsActive)?.Id
                                       ?? FiscalYears.FirstOrDefault()?.Id ?? 0;
        }, "در حال بارگذاری...");
        Recompute();
    }

    partial void OnSaleAmountChanged(decimal value) => Recompute();
    partial void OnCostAmountChanged(decimal value) => Recompute();
    partial void OnCommissionPercentChanged(decimal value) => Recompute();
    partial void OnPaidAmountChanged(decimal value) => Recompute();

    private static string RoleLabel(TourismAccountRole r) => r switch
    {
        TourismAccountRole.Cash => "صندوق",
        TourismAccountRole.Receivable => "دریافتنی مشتریان",
        TourismAccountRole.TourismRevenue => "درآمدِ گردشگری",
        TourismAccountRole.TourismCost => "بهای تمام‌شدهٔ خدمات",
        TourismAccountRole.SupplierPayable => "پرداختنی تأمین‌کننده",
        TourismAccountRole.CommissionExpense => "هزینهٔ کمیسیون",
        TourismAccountRole.CommissionPayable => "کمیسیون پرداختنی",
        _ => r.ToString()
    };

    /// <summary>پیش‌نمایشِ زنده — موتورِ خالصِ تسویه را مستقیم صدا می‌زند (همان منطقِ بک‌اند).</summary>
    private void Recompute()
    {
        Lines.Clear();
        if (SaleAmount <= 0)
        { Commission = NetProfit = TotalDebit = TotalCredit = 0; IsBalanced = false; return; }

        var r = TourismSettlement.Build(SaleAmount, CostAmount, CommissionPercent, PaidAmount);
        foreach (var l in r.Lines)
            Lines.Add(new TourismVoucherPreviewRow(RoleLabel(l.Role), l.Debit, l.Credit, l.Description));
        Commission = r.Commission; NetProfit = r.NetProfit;
        TotalDebit = r.TotalDebit; TotalCredit = r.TotalCredit; IsBalanced = r.IsBalanced;
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (SaleAmount <= 0) { await _dialogService.ShowWarningAsync("مبلغِ فروش را وارد کنید."); return; }
        if (SelectedFiscalYearId <= 0) { await _dialogService.ShowWarningAsync("سالِ مالی را انتخاب کنید."); return; }
        if (string.IsNullOrWhiteSpace(BookingRef)) { await _dialogService.ShowWarningAsync("شمارهٔ رزرو/مرجع را وارد کنید."); return; }

        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new GenerateTourismVoucherCommand(
                _user.BranchId ?? 1, SelectedFiscalYearId, Date, BookingRef.Trim(),
                SaleAmount, CostAmount, CommissionPercent, PaidAmount, PaymentMethod));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync(
                $"سندِ حسابداریِ گردشگری برای رزروِ «{BookingRef}» صادر شد (سند #{res.Value}). " +
                $"سودِ خالص {NetProfit:N0} ریال.");
            // پاک‌سازی برای رزروِ بعدی (تاریخ/سالِ مالی بماند)
            BookingRef = string.Empty; SaleAmount = CostAmount = CommissionPercent = PaidAmount = 0;
        }, "در حال صدورِ سند...");
    }
}
