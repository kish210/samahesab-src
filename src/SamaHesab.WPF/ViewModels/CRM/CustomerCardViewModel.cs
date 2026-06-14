using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.BI.Queries;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.CRM;

/// <summary>کارت ۳۶۰° مشتری طبق design-system (customer-card.html): شناسنامه + اعتبار + KPI + گردش حساب.</summary>
public partial class CustomerCardViewModel : BaseViewModel, SamaHesab.WPF.Services.INavigationAware
{
    private readonly IMediator _mediator;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<SamaHesab.Domain.Entities.Accounting.Cheque> _cheques;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;

    // ── شناسنامه ──
    [ObservableProperty] private int _customerId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _initials = string.Empty;
    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _groupLabel = string.Empty;
    [ObservableProperty] private string? _mobile;
    [ObservableProperty] private string? _phone;
    [ObservableProperty] private string? _nationalCode;
    [ObservableProperty] private string? _economicCode;
    [ObservableProperty] private string? _contactPerson;   // کارِ ۱۰: شخصِ رابط
    [ObservableProperty] private string? _visitor;          // کارِ ۱۰: ویزیتور
    [ObservableProperty] private string? _address;
    [ObservableProperty] private string _statusLabel = "فعال";

    // ── اعتبار / مانده ──
    [ObservableProperty] private decimal _balance;
    [ObservableProperty] private decimal _creditLimit;
    [ObservableProperty] private bool _unlimitedCredit;
    [ObservableProperty] private double _creditPercent;       // 0..100 برای نوار
    [ObservableProperty] private string _creditPercentLabel = "۰٪";
    [ObservableProperty] private bool _isOverCredit;

    // ── KPI ──
    [ObservableProperty] private decimal _totalSales;
    [ObservableProperty] private int _invoiceCount;
    [ObservableProperty] private decimal _averagePerInvoice;
    [ObservableProperty] private int _loyaltyPoints;
    [ObservableProperty] private int _settlementDays;      // R16: مهلت/میانگین تسویه (روز)
    [ObservableProperty] private decimal _chequeInProgress;  // R16/کارِ۱۰: مجموعِ چکِ دریافتیِ در جریانِ مشتری
    [ObservableProperty] private string? _lastInvoiceDate;

    // ── گردش حساب ──
    public ObservableCollection<LedgerRow> Ledger { get; } = new();
    [ObservableProperty] private decimal _ledgerTotalDebit;
    [ObservableProperty] private decimal _ledgerTotalCredit;
    [ObservableProperty] private decimal _ledgerClosing;
    [ObservableProperty] private bool _hasData;

    public CustomerCardViewModel(IMediator mediator, IRepository<Customer> customers,
        IRepository<SamaHesab.Domain.Entities.Accounting.Cheque> cheques,
        ICurrentUserService currentUser, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _customers = customers; _cheques = cheques;
        _currentUser = currentUser; _calendar = calendar;
    }

    public async Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is int id && id > 0) await LoadForAsync(id);
        else await LoadAsync();
    }

    /// <summary>پیش‌فرض: اولین مشتری فعال (برای منو/پیش‌نمایش).</summary>
    public override async Task LoadAsync()
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var first = (await _customers.FindAsync(c => c.CompanyId == companyId && c.IsActive)).FirstOrDefault();
        if (first != null) await LoadForAsync(first.Id);
    }

    public async Task LoadForAsync(int customerId)
    {
        await ExecuteAsync(async () =>
        {
            var c = await _customers.GetByIdAsync(customerId);
            if (c == null) { await _dialogService.ShowErrorAsync("مشتری یافت نشد."); return; }

            CustomerId = c.Id;
            Name = c.FullName;
            Initials = BuildInitials(c.FullName);
            Code = c.Code;
            GroupLabel = string.IsNullOrWhiteSpace(c.PriceLevel) ? c.CustomerType : $"{c.CustomerType} · {c.PriceLevel}";
            Mobile = c.Mobile; Phone = c.Phone;
            NationalCode = c.NationalCode; EconomicCode = c.EconomicCode;
            ContactPerson = c.ContactPerson; Visitor = c.Visitor;
            Address = string.Join("، ", new[] { c.Province, c.City, c.Address }.Where(s => !string.IsNullOrWhiteSpace(s)));
            LoyaltyPoints = c.LoyaltyPoints;
            SettlementDays = c.CreditDays;        // R16: مهلت تسویه (روز)
            StatusLabel = c.IsActive ? "فعال" : "غیرفعال";

            // R16/کارِ ۱۰: چکِ دریافتیِ در جریانِ این مشتری (بدونِ تغییرِ اسکیما — از PartyId/Status)
            var inProc = await _cheques.FindAsync(ch => ch.PartyId == customerId
                && ch.ChequeType == SamaHesab.Domain.Enums.ChequeType.Received
                && ch.Status == SamaHesab.Domain.Enums.ChequeStatus.InProcess);
            ChequeInProgress = inProc.Sum(x => x.Amount);

            // اعتبار
            var credit = await _mediator.Send(new GetCustomerCreditQuery(customerId));
            Balance = credit?.Balance ?? c.Balance;
            CreditLimit = credit?.CreditLimit ?? c.CreditLimit;
            UnlimitedCredit = CreditLimit <= 0;
            IsOverCredit = credit?.IsOverLimit ?? false;
            var pct = (!UnlimitedCredit && CreditLimit > 0) ? (double)(Balance / CreditLimit) * 100 : 0;
            if (pct < 0) pct = 0; if (pct > 100) pct = 100;
            CreditPercent = pct;
            CreditPercentLabel = ValueConvertersToPersian($"{pct:0}٪");

            // KPI — بازه‌ی سال جاری شمسی (فروردین تا امروز)
            var today = _calendar.GetCurrentPersianDate();
            var yearStart = (today.Length >= 4 ? today.Substring(0, 4) : "1405") + "/01/01";
            var an = await _mediator.Send(new GetCustomerAnalyticsQuery(customerId, yearStart, today));
            TotalSales = an.TotalSales;
            InvoiceCount = an.InvoiceCount;
            AveragePerInvoice = an.AveragePerInvoice;
            LastInvoiceDate = an.LastInvoiceDate;

            // گردش حساب
            Ledger.Clear();
            var st = await _mediator.Send(new GetCustomerStatementQuery(customerId));
            if (st.Succeeded && st.Value != null)
            {
                foreach (var r in st.Value.Rows)
                    Ledger.Add(new LedgerRow(r.Date, r.DocNumber, r.Description, r.Debit, r.Credit, r.Balance,
                        r.Balance >= 0 ? "بد" : "بس"));
                LedgerTotalDebit = st.Value.TotalDebit;
                LedgerTotalCredit = st.Value.TotalCredit;
                LedgerClosing = st.Value.ClosingBalance;
            }
            HasData = true;
        }, "در حال بارگذاری کارت مشتری...");
    }

    private static string BuildInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "—";
        if (parts.Length == 1) return parts[0].Length >= 2 ? parts[0].Substring(0, 2) : parts[0];
        return $"{parts[0][0]}{parts[1][0]}";
    }

    private static string ValueConvertersToPersian(string s)
        => SamaHesab.WPF.Converters.NumberFormatConverter.ToPersian(s);

    [RelayCommand] private void Edit() => _navigationService.NavigateTo("CustomerEdit", CustomerId);
    [RelayCommand] private void NewInvoice() => _navigationService.NavigateTo("SalesInvoiceEdit");
    [RelayCommand] private async Task Receipt() => await _dialogService.ShowInfoAsync("ثبت دریافت وجه از مشتری…");
    [RelayCommand] private async Task PrintStatement() => await _dialogService.ShowInfoAsync("در حال آماده‌سازی صورت‌حساب…");
    [RelayCommand] private async Task SmsBalance() => await _dialogService.ShowInfoAsync($"پیامک مانده ({Balance:N0}) به {Mobile}…");
}

/// <summary>ردیف گردش حساب مشتری (برای گرید کارت ۳۶۰°).</summary>
public record LedgerRow(string Date, string DocNumber, string Description,
    decimal Debit, decimal Credit, decimal Balance, string Side);
