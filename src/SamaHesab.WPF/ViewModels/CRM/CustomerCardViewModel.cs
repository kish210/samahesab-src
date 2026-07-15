using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.Application.BI.Queries;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Sales.Queries;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.CRM;

/// <summary>کارت ۳۶۰° مشتری طبق design-system (customer-card.html): شناسنامه + اعتبار + KPI + گردش حساب.</summary>
public partial class CustomerCardViewModel : BaseViewModel, SamaHesab.WPF.Services.INavigationAware
{
    private readonly IMediator _mediator;
    private readonly ApiClient _api;
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
    // UX-CRM-SUPPLIER-2: پیش‌تر کارتِ مشترک نمی‌دانست این شخص تأمین‌کننده هم هست یا نه.
    [ObservableProperty] private bool _isCustomer = true;
    [ObservableProperty] private bool _isSupplier;

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

    // ── تحلیل خرید (روند ماهانه + پرخریدترین کالاها) ──
    public ObservableCollection<TopProductRow> TopProducts { get; } = new();
    public ObservableCollection<TrendBar> MonthlyTrend { get; } = new();
    [ObservableProperty] private string? _firstInvoiceDate;

    // ── گردش حساب ──
    public ObservableCollection<LedgerRow> Ledger { get; } = new();
    [ObservableProperty] private decimal _ledgerTotalDebit;
    [ObservableProperty] private decimal _ledgerTotalCredit;
    [ObservableProperty] private decimal _ledgerClosing;
    [ObservableProperty] private bool _hasData;

    // ── مینی‌تب‌ها: گردش حساب / فاکتورها / چک‌ها / تماس‌وپیگیری / اسناد ضمیمه ──
    [ObservableProperty] private int _selectedTab;
    public ObservableCollection<SalesInvoiceRowDto> Invoices { get; } = new();
    public ObservableCollection<SamaHesab.Application.Purchase.Queries.PurchaseInvoiceRowDto> PurchaseInvoices { get; } = new();
    public ObservableCollection<CustomerChequeRow> Cheques { get; } = new();

    [RelayCommand]
    private void SelectTab(string? tab) => SelectedTab = int.TryParse(tab, out var t) ? t : 0;

    public CustomerCardViewModel(IMediator mediator, ApiClient api,
        ICurrentUserService currentUser, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _api = api;
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
        // 🏛️ کلاینت→API، دسکتاپ→Application — اولین مشتری برای پیش‌نمایش
        var first = (!string.IsNullOrWhiteSpace(_api.BaseUrl)
            ? (await _api.GetCustomersAsync()).Select(c => c.Id)
            : (await _mediator.Send(new GetCustomersQuery())).Select(c => c.Id)).FirstOrDefault();
        if (first > 0) await LoadForAsync(first);
    }

    public async Task LoadForAsync(int customerId)
    {
        await ExecuteAsync(async () =>
        {
            // 🏛️ شناسنامه + چکِ در جریان از کوئریِ تجمیعی (API/Application)
            CustomerCardDto? c;
            if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
            {
                var a = await _api.GetCustomerCardAsync(customerId);
                c = a == null ? null : new CustomerCardDto(a.Id, a.Name, a.Code, a.CustomerType, a.PriceLevel,
                    a.Mobile, a.Phone, a.NationalCode, a.EconomicCode, a.ContactPerson, a.Visitor,
                    a.Province, a.City, a.Address, a.LoyaltyPoints, a.CreditDays, a.IsActive,
                    a.Balance, a.CreditLimit, a.ChequeInProgress, a.IsCustomer, a.IsSupplier);
            }
            else c = await _mediator.Send(new GetCustomerCardQuery(customerId));
            if (c == null) { await _dialogService.ShowErrorAsync("مشتری یافت نشد."); return; }

            SelectedTab = 0;
            CustomerId = c.Id;
            Name = c.Name;
            Initials = BuildInitials(c.Name);
            Code = c.Code;
            GroupLabel = string.IsNullOrWhiteSpace(c.PriceLevel) ? c.CustomerType : $"{c.CustomerType} · {c.PriceLevel}";
            Mobile = c.Mobile; Phone = c.Phone;
            NationalCode = c.NationalCode; EconomicCode = c.EconomicCode;
            ContactPerson = c.ContactPerson; Visitor = c.Visitor;
            Address = string.Join("، ", new[] { c.Province, c.City, c.Address }.Where(s => !string.IsNullOrWhiteSpace(s)));
            LoyaltyPoints = c.LoyaltyPoints;
            SettlementDays = c.CreditDays;        // R16: مهلت تسویه (روز)
            StatusLabel = c.IsActive ? "فعال" : "غیرفعال";
            ChequeInProgress = c.ChequeInProgress;
            IsCustomer = c.IsCustomer; IsSupplier = c.IsSupplier;

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
            FirstInvoiceDate = an.FirstInvoiceDate;

            // پرخریدترین کالاها (۵ تای برتر بر اساسِ مبلغ)
            TopProducts.Clear();
            foreach (var p in an.TopProducts.Take(5))
                TopProducts.Add(new TopProductRow(p.Name, p.Total, p.LineCount));

            // روندِ ماهانهٔ خرید — عرضِ نوار نسبت به بیشترین ماه (حداکثر ۱۱۰px).
            MonthlyTrend.Clear();
            var maxMonth = an.MonthlyTrend.Count == 0 ? 0m : an.MonthlyTrend.Max(t => t.Total);
            foreach (var t in an.MonthlyTrend)
            {
                var width = maxMonth <= 0 ? 0d : (double)(t.Total / maxMonth) * 110d;
                if (width is > 0 and < 3) width = 3;   // حداقلِ دیداری
                MonthlyTrend.Add(new TrendBar(t.Period, t.Total, t.Count, width));
            }

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

            // فاکتورهای فروشِ همین مشتری
            Invoices.Clear();
            foreach (var inv in await _mediator.Send(new GetSalesInvoicesQuery(CustomerId: customerId)))
                Invoices.Add(inv);

            // UX-CRM-SUPPLIER-2: فاکتورهایِ خریدِ همین شخص، فقط وقتی که واقعاً تأمین‌کننده هم هست.
            PurchaseInvoices.Clear();
            if (IsSupplier)
                foreach (var pinv in await _mediator.Send(new SamaHesab.Application.Purchase.Queries.GetPurchaseInvoicesQuery(SupplierId: customerId)))
                    PurchaseInvoices.Add(pinv);

            // چک‌های ثبت‌شده به‌نامِ همین مشتری
            Cheques.Clear();
            foreach (var chq in await _mediator.Send(new GetChequesQuery(PartyId: customerId)))
                Cheques.Add(CustomerChequeRow.From(chq));

            await ReloadAttachmentsAsync(customerId);
            HasData = true;
        }, "در حال بارگذاری کارت مشتری...");
    }

    // ── کارِ ۹: اسنادِ ضمیمهٔ مشتری (فایل در AppData، متادیتا در DB) ──
    public ObservableCollection<SamaHesab.Application.CRM.Queries.CustomerAttachmentDto> Attachments { get; } = new();

    private async Task ReloadAttachmentsAsync(int customerId)
    {
        Attachments.Clear();
        foreach (var a in await _mediator.Send(new SamaHesab.Application.CRM.Queries.GetCustomerAttachmentsQuery(customerId)))
            Attachments.Add(a);
    }

    private static string AttachmentsDir(int companyId, int customerId)
    {
        var dir = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "SamaHesab", "attachments", companyId.ToString(), customerId.ToString());
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    [RelayCommand]
    private async Task AddAttachmentAsync()
    {
        if (CustomerId <= 0) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "انتخاب فایلِ ضمیمه",
            Filter = "همهٔ فایل‌ها|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;

        await ExecuteAsync(async () =>
        {
            var companyId = _currentUser.CompanyId ?? 1;
            var dir = AttachmentsDir(companyId, CustomerId);
            foreach (var src in dlg.FileNames)
            {
                try
                {
                    var info = new System.IO.FileInfo(src);
                    var dest = System.IO.Path.Combine(dir, $"{System.Guid.NewGuid():N}_{info.Name}");
                    System.IO.File.Copy(src, dest, overwrite: false);
                    await _mediator.Send(new SamaHesab.Application.CRM.Commands.AddCustomerAttachmentCommand(
                        CustomerId, info.Name, dest, GuessContentType(info.Extension), info.Length,
                        _calendar.GetCurrentPersianDate()));
                }
                catch (System.Exception ex) { await _dialogService.ShowErrorAsync($"خطا در افزودن «{System.IO.Path.GetFileName(src)}»: {ex.Message}"); }
            }
            await ReloadAttachmentsAsync(CustomerId);
        }, "در حال افزودنِ پیوست...");
    }

    [RelayCommand]
    private async Task OpenAttachmentAsync(SamaHesab.Application.CRM.Queries.CustomerAttachmentDto? a)
    {
        if (a is null) return;
        if (!System.IO.File.Exists(a.StoredPath)) { await _dialogService.ShowErrorAsync("فایل یافت نشد (احتمالاً جابه‌جا/حذف شده)."); return; }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(a.StoredPath) { UseShellExecute = true }); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private async Task DeleteAttachmentAsync(SamaHesab.Application.CRM.Queries.CustomerAttachmentDto? a)
    {
        if (a is null) return;
        if (!await _dialogService.ConfirmAsync($"حذفِ «{a.FileName}»؟")) return;
        var res = await _mediator.Send(new SamaHesab.Application.CRM.Commands.DeleteCustomerAttachmentCommand(a.Id));
        if (res.Succeeded)
        {
            try { if (!string.IsNullOrEmpty(res.Value) && System.IO.File.Exists(res.Value)) System.IO.File.Delete(res.Value); } catch { /* فایلِ فیزیکی اختیاری */ }
            await ReloadAttachmentsAsync(CustomerId);
        }
        else await _dialogService.ShowErrorAsync(res.ErrorMessage);
    }

    private static string GuessContentType(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png",
        ".xlsx" or ".xls" => "application/vnd.ms-excel",
        ".docx" or ".doc" => "application/msword",
        _ => "application/octet-stream"
    };

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
    // کلیدِ صفحهٔ فاکتورِ فروش «SalesInvoice» است (نه SalesInvoiceEdit) — باگِ قبلی: دکمه کاری نمی‌کرد.
    // باگِ بعدیِ کشف‌شده (@2026-07-10): پاس‌دادنِ CustomerId به‌صورتِ int خام باعث می‌شد
    // SalesInvoiceEditViewModel آن را با شناسهٔ فاکتور اشتباه بگیرد (فاکتورِ فردِ دیگری بار می‌شد
    // یا فرم خالی می‌ماند). حالا با PreselectCustomerParam درست تفکیک می‌شود.
    // UX-CRM-SUPPLIER-2: پیش‌تر این دکمه برایِ شخصی که فقط تأمین‌کننده بود هم فاکتورِ فروش باز
    // می‌کرد (طرفِ اشتباه). حالا اگر شخص فقط تأمین‌کننده است (نه خریدار)، فاکتورِ خرید باز می‌شود.
    [RelayCommand]
    private void NewInvoice()
    {
        if (IsSupplier && !IsCustomer)
            _navigationService.NavigateTo("PurchaseInvoice", new SamaHesab.WPF.ViewModels.Purchase.PreselectSupplierParam(CustomerId));
        else
            _navigationService.NavigateTo("SalesInvoice", new SamaHesab.WPF.ViewModels.Sales.PreselectCustomerParam(CustomerId));
    }
    // دریافت/پرداختِ وجه — باگِ رفع‌شده (@2026-07-10): قبلاً به صفحهٔ لیستِ «Receivables» می‌رفت که
    // پارامترِ مشتری را اصلاً نمی‌خواند (INavigationAware نداشت) و سندِ حسابداری هم ثبت نمی‌کرد.
    // حالا به صفحهٔ واقعیِ «دریافت و پرداختِ وجه» می‌رود که سندِ حسابداری می‌سازد.
    [RelayCommand] private void Receipt()
        => _navigationService.NavigateTo("ReceivePayment", new SamaHesab.WPF.ViewModels.Treasury.PreselectPartyParam(CustomerId, IsReceive: true));
    [RelayCommand] private void Payment()
        => _navigationService.NavigateTo("ReceivePayment", new SamaHesab.WPF.ViewModels.Treasury.PreselectPartyParam(CustomerId, IsReceive: false));
    [RelayCommand]
    private async Task PrintStatement()
    {
        if (Ledger.Count == 0) { await _dialogService.ShowWarningAsync("گردشی برای چاپ نیست."); return; }
        try
        {
            string N(decimal d) => d.ToString("N0");
            var table = new SamaHesab.Application.Reports.Export.ReportTable(
                $"صورت‌حساب مشتری — {Name} (مانده: {N(LedgerClosing)})",
                new[] { "تاریخ", "شماره سند", "شرح", "بدهکار", "بستانکار", "مانده" },
                Ledger.Select(r => new[] { r.Date, r.DocNumber, r.Description, N(r.Debit), N(r.Credit), N(r.Balance) }).ToList());
            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"صورت‌حساب_{Name}_{System.DateTime.Now:yyyyMMdd_HHmmss}.html");
            System.IO.File.WriteAllText(path, SamaHesab.Application.Reports.Export.ReportExporter.ToHtml(table), new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }
    [RelayCommand] private async Task SmsBalance() => await _dialogService.ShowInfoAsync($"پیامک مانده ({Balance:N0}) به {Mobile}…");
}

/// <summary>ردیف گردش حساب مشتری (برای گرید کارت ۳۶۰°).</summary>
public record LedgerRow(string Date, string DocNumber, string Description,
    decimal Debit, decimal Credit, decimal Balance, string Side);

/// <summary>ردیفِ پرخریدترین کالاهای مشتری (تحلیل خرید).</summary>
public record TopProductRow(string Name, decimal Total, int LineCount);

/// <summary>ماهِ روندِ خرید + عرضِ نوار (px) برای نمودارِ میله‌ای ساده.</summary>
public record TrendBar(string Period, decimal Total, int Count, double BarWidth);

/// <summary>ردیفِ چکِ مشتری با برچسبِ فارسیِ نوع/وضعیت (برای تبِ «چک‌ها» در کارتِ مشتری).</summary>
public record CustomerChequeRow(int Id, string TypeLabel, string ChequeNumber, string BankName,
    decimal Amount, string DueDate, string StatusLabel)
{
    public static CustomerChequeRow From(ChequeRowDto c) => new(c.Id,
        c.ChequeType == ChequeType.Received ? "دریافتی" : "پرداختی",
        c.ChequeNumber, c.BankName, c.Amount, c.DueDate,
        c.Status switch
        {
            ChequeStatus.InProcess => "در جریان",
            ChequeStatus.Cleared => "وصول شده",
            ChequeStatus.Returned => "برگشتی",
            ChequeStatus.Transferred => "واگذار به بانک",
            ChequeStatus.Cancelled => "ابطال شده",
            _ => "نامشخص"
        });
}
