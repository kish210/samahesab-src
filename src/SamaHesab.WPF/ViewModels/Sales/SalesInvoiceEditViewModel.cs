using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Application.Documents;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.Application.Sales.Commands;
using SamaHesab.Domain.Enums;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Sales;

public partial class SalesInvoiceEditViewModel : BaseViewModel, SamaHesab.WPF.Services.INavigationAware
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly ApiClient _api;
    private readonly IPersianCalendarService _calendar;
    // U-ACCT-1.7 — قبلاً FiscalYearId در صدور/برگشتِ فاکتورِ فروش هاردکد رویِ ۱ بود؛ برایِ هر
    // شرکتی که سالِ مالیِ فعالش Id≠۱ باشد، فاکتور به سالِ مالیِ اشتباه/بسته متصل می‌شد.
    private int _activeFiscalYearId = 1;

    [ObservableProperty] private string _invoiceNumber = "--- خودکار ---";
    [ObservableProperty] private bool _autoNumber = true;            // شمارهٔ خودکار (مثلِ تصویر)
    [ObservableProperty] private string _reference = string.Empty;   // ارجاع
    [ObservableProperty] private string _title = string.Empty;       // عنوانِ فاکتور
    [ObservableProperty] private int? _selectedProjectId;            // پروژه
    [ObservableProperty] private string _invoiceDate = string.Empty;
    [ObservableProperty] private int _selectedCustomerId;

    partial void OnAutoNumberChanged(bool value)
    { InvoiceNumber = value ? "--- خودکار ---" : (InvoiceNumber == "--- خودکار ---" ? string.Empty : InvoiceNumber); }
    [ObservableProperty] private string _selectedCustomerName = string.Empty;
    [ObservableProperty] private int _selectedWarehouseId;
    [ObservableProperty] private string _invoiceType = "فروش";
    [ObservableProperty] private string _priceLevel = "خرده";
    [ObservableProperty] private string? _dueDate;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string _productSearch = string.Empty;
    [ObservableProperty] private string _barcodeInput = string.Empty;   // ورود سریع کیبوردمحور (بارکد/کد)
    [ObservableProperty] private decimal _addQty = 1;
    [ObservableProperty] private decimal _addUnitPrice;
    [ObservableProperty] private decimal _addDiscountPct;
    [ObservableProperty] private decimal _addTaxPct;
    [ObservableProperty] private SalesInvoiceItemRow? _selectedItem;
    [ObservableProperty] private decimal _subTotal;
    [ObservableProperty] private decimal _totalDiscount;
    [ObservableProperty] private decimal _totalTax;
    [ObservableProperty] private decimal _shipping;
    [ObservableProperty] private decimal _otherCosts;
    [ObservableProperty] private decimal _grandTotal;
    [ObservableProperty] private decimal _paidAmount;
    [ObservableProperty] private decimal _remainAmount;
    [ObservableProperty] private string _paymentType = "نقدی";
    [ObservableProperty] private decimal _invoiceDiscount;
    [ObservableProperty] private decimal _commissionPercent;

    // ── اطلاعات اعتباری مشتری (طبق design-system: نوار زیر هدر فاکتور) ──
    [ObservableProperty] private bool _hasCustomerInfo;
    [ObservableProperty] private decimal _customerBalance;
    [ObservableProperty] private decimal _customerCreditLimit;
    [ObservableProperty] private bool _customerUnlimitedCredit;
    [ObservableProperty] private bool _isOverCredit;

    [ObservableProperty] private ProductSearchResult? _selectedProductItem;

    /// <summary>T10 — پس از افزودنِ هر ردیف، View نوارِ ورود را دوباره فوکوس می‌کند (ورودِ پیوستهٔ کیبوردمحور).</summary>
    public event System.Action? RowAdded;

    public ObservableCollection<SalesInvoiceItemRow> InvoiceItems { get; } = new();
    /// <summary>فاز ۱۰ DT-3 — قالب‌های چاپِ فاکتور فروش (برای دکمهٔ «چاپ ▼»).</summary>
    public ObservableCollection<DocumentTemplateListDto> PrintTemplates { get; } = new();
    public ObservableCollection<ProductSearchResult> SearchResults { get; } = new();
    /// <summary>مشتریان اخیرِ این کاربر (کار #۳۹) — چیپ‌های دسترسی سریع.</summary>
    public ObservableCollection<RecentRef> RecentCustomers { get; } = new();
    public List<ProductSearchResult> AllProducts { get; private set; } = new();
    public List<CustomerItem> Customers { get; private set; } = new();
    public List<WarehouseItem> Warehouses { get; private set; } = new();
    public List<ProjectItem> Projects { get; private set; } = new();
    public List<string> InvoiceTypes { get; } = new() { "فروش", "برگشت از فروش", "پیش‌فاکتور" };
    public List<string> PriceLevels { get; } = new() { "خرده", "عمده", "ویژه" };
    public List<string> PaymentTypes { get; } = new() { "نقدی", "کارتخوان", "ترکیبی", "چک", "نسیه", "اقساط" };


    /// <summary>تعدادِ کلِ اقلامِ واقعی (ردیف‌های دارای کالا) — برای نوارِ جمع.</summary>
    [ObservableProperty] private decimal _totalQuantity;

    private readonly IPrintService _printService;
    private readonly IBarcodeService _barcode;   // L6 — تصویرِ QR برای چاپِ قالبی
    private readonly SamaHesab.Application.Payments.IPaymentTerminalService _terminal;   // 💳 CR-1

    /// <summary>موجودیِ انبارِ انتخابی به‌ازای هر کالا (OPT-5: نمایشِ موجودی هنگام فروش).</summary>
    private Dictionary<int, decimal> _onHand = new();
    /// <summary>موجودیِ کالایِ نوارِ ورود (کالای انتخاب/اسکن‌شده).</summary>
    [ObservableProperty] private decimal _entryOnHand;

    // 💳 CR-1 — نتیجهٔ پرداختِ کارت‌خوان
    [ObservableProperty] private string? _cardPaymentInfo;
    public bool IsCardPayment => PaymentType is "کارتخوان";

    // 💳 POS-IR-3 — پرداختِ ترکیبی (نقد + کارت + نسیه)
    /// <summary>سهمِ نقدِ پرداختِ ترکیبی.</summary>
    [ObservableProperty] private decimal _cashAmount;
    /// <summary>سهمِ کارت‌خوانِ پرداختِ ترکیبی (با CR-1 دریافت می‌شود).</summary>
    [ObservableProperty] private decimal _cardAmount;
    public bool IsMixedPayment => PaymentType is "ترکیبی";

    // 🔁 مرجوعی (برگشت از فروش) / 📄 پیش‌فاکتور — برچسبِ دکمهٔ ثبت متناسب می‌شود.
    public bool IsReturnInvoice => InvoiceType == "برگشت از فروش";
    public bool IsQuotationInvoice => InvoiceType == "پیش‌فاکتور";

    // 👁 UX-SALES-VIEW — حالتِ مشاهدهٔ فاکتورِ ثبت‌شده (ثبتِ دوباره مجاز نیست).
    [ObservableProperty] private bool _isViewingExisting;
    partial void OnIsViewingExistingChanged(bool value) => OnPropertyChanged(nameof(PostButtonText));

    public string PostButtonText => IsViewingExisting ? "👁 فاکتورِ ثبت‌شده — فقط مشاهده/چاپ"
        : IsReturnInvoice ? "↩ ثبت برگشت از فروش — F9"
        : IsQuotationInvoice ? "📄 ثبت پیش‌فاکتور — F9"
        : "✓ ثبت نهایی فاکتور — F9";
    partial void OnInvoiceTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsReturnInvoice));
        OnPropertyChanged(nameof(IsQuotationInvoice));
        OnPropertyChanged(nameof(PostButtonText));
    }

    /// <summary>
    /// UX-SALES-VIEW — بازکردنِ فاکتورِ موجود از فهرست (Param=Id) در حالتِ مشاهده.
    /// باگِ رفع‌شده @2026-07-10: قبلاً هر پارامترِ int (از جمله CustomerId که کارتِ مشتری برایِ
    /// پیش‌انتخابِ مشتری در «فاکتورِ جدید» می‌فرستد) این‌جا به‌اشتباه به‌عنوانِ شناسهٔ فاکتور خوانده
    /// می‌شد — یا فاکتورِ کاملاً نامرتبطِ دیگری (با مشتریِ متفاوت) بار می‌شد، یا اگر آن id به هیچ
    /// فاکتوری نمی‌خورد، خطایِ «فاکتور یافت نشد» می‌داد و فرم خالی می‌ماند. حالا با نوعِ
    /// <see cref="PreselectCustomerParam"/> از حالتِ «بازکردنِ فاکتورِ موجود» تفکیک می‌شود.
    /// </summary>
    public async Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is int id && id > 0) await LoadExistingAsync(id);
        else if (parameter is PreselectCustomerParam pc && pc.CustomerId > 0)
        {
            NewInvoice();   // اگر تبِ فاکتورِ فروش از قبل باز بود، ابتدا کاملاً به حالتِ خالی/جدید برگرد
            SelectedCustomerId = pc.CustomerId;
        }
    }

    private async Task LoadExistingAsync(int id)
    {
        await ExecuteAsync(async () =>
        {
            var d = await _mediator.Send(new SamaHesab.Application.Sales.Queries.GetSalesInvoiceByIdQuery(id));
            if (d == null) { await _dialogService.ShowErrorAsync("فاکتور یافت نشد."); return; }

            AutoNumber = false; InvoiceNumber = d.Number; InvoiceDate = d.Date;
            SelectedCustomerId = d.CustomerId; SelectedWarehouseId = d.WarehouseId;
            PriceLevel = d.PriceLevel; InvoiceType = d.InvoiceType;
            Shipping = d.Shipping; OtherCosts = d.OtherCosts;
            InvoiceDiscount = d.InvoiceDiscount;
            DueDate = d.DueDate; Description = d.Description;
            Reference = d.Reference ?? string.Empty; Title = d.Title ?? string.Empty;

            InvoiceItems.Clear();
            foreach (var it in d.Items)
            {
                var row = new SalesInvoiceItemRow
                {
                    ProductId = it.ProductId, ProductCode = it.Code, ProductName = it.Name,
                    Quantity = it.Quantity, UnitPrice = it.UnitPrice,
                    DiscountPct = it.DiscountPct, TaxPct = it.TaxPct, Description = it.Description
                };
                row.Recalculate();
                InvoiceItems.Add(row);
            }
            RenumberRows();
            RecalculateTotals();
            PaidAmount = d.PaidAmount;
            IsViewingExisting = true;   // قفلِ ثبتِ دوباره
        }, "در حال بازکردنِ فاکتور...");
    }
    /// <summary>باقیماندهٔ نسیه در پرداختِ ترکیبی (فقط برای نمایش؛ منفی=اضافه‌پرداخت).</summary>
    public decimal MixedCreditAmount => GrandTotal - CashAmount - CardAmount;

    partial void OnPaymentTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsCardPayment));
        OnPropertyChanged(nameof(IsMixedPayment));
        if (value is "ترکیبی")
        {
            // پیش‌فرض: کلِ مبلغ نقد، کارت ۰ — کاربر تفکیک می‌کند.
            CashAmount = GrandTotal;
            CardAmount = 0;
        }
    }

    partial void OnCashAmountChanged(decimal value) => SyncMixedPaid();
    partial void OnCardAmountChanged(decimal value) => SyncMixedPaid();

    /// <summary>در حالتِ ترکیبی: پرداختی = نقد + کارت؛ مابقی خودکار نسیه می‌شود.</summary>
    private void SyncMixedPaid()
    {
        if (!IsMixedPayment) return;
        if (CashAmount < 0) CashAmount = 0;
        if (CardAmount < 0) CardAmount = 0;
        PaidAmount = CashAmount + CardAmount;
        OnPropertyChanged(nameof(MixedCreditAmount));
    }

    public SalesInvoiceEditViewModel(IMediator mediator, ICurrentUserService currentUser,
        ApiClient api,
        IDialogService dialogService,
        INavigationService navigationService, IPersianCalendarService calendar,
        IPrintService printService, IBarcodeService barcode,
        SamaHesab.Application.Payments.IPaymentTerminalService terminal)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _currentUser = currentUser;
        _api = api; _calendar = calendar;
        _printService = printService; _barcode = barcode; _terminal = terminal;
    }

    /// <summary>💳 CR-1 — دریافتِ مبلغِ فاکتور با کارت‌خوانِ بانکی؛ در صورتِ تأیید، پرداختی پر و RRN ثبت می‌شود.</summary>
    [RelayCommand]
    private async Task ChargeCardAsync()
    {
        if (GrandTotal <= 0) { await _dialogService.ShowWarningAsync("مبلغِ فاکتور صفر است."); return; }
        await ExecuteAsync(async () =>
        {
            var res = await _terminal.PayAsync(new SamaHesab.Application.Payments.CardPaymentRequest(GrandTotal, InvoiceNumber));
            if (!res.Approved) { CardPaymentInfo = "❌ " + res.Message; await _dialogService.ShowErrorAsync(res.Message); return; }

            PaidAmount = res.Amount;
            RemainAmount = GrandTotal - res.Amount;
            // ثبتِ مرجعِ تراکنش روی فاکتور (در «ارجاع»).
            Reference = $"کارت‌خوان RRN:{res.Rrn}";
            CardPaymentInfo = $"✔ تأیید شد · پایانه {res.TerminalId} · پیگیری {res.TraceNo} · کارت {res.MaskedPan} · RRN {res.Rrn}";
        }, "در حال ارتباط با کارت‌خوان...");
    }

    /// <summary>💳 POS-IR-3 — دریافتِ «سهمِ کارت»ِ پرداختِ ترکیبی با کارت‌خوان. اگر سهمِ کارت ۰ باشد، باقیماندهٔ مبلغ (مبلغِ کل منهای نقد) را می‌گیرد.</summary>
    [RelayCommand]
    private async Task ChargeCardShareAsync()
    {
        var amount = CardAmount > 0 ? CardAmount : GrandTotal - CashAmount;
        if (amount <= 0) { await _dialogService.ShowWarningAsync("سهمِ کارت صفر است؛ ابتدا مبلغِ کارت یا نقد را تنظیم کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var res = await _terminal.PayAsync(new SamaHesab.Application.Payments.CardPaymentRequest(amount, InvoiceNumber));
            if (!res.Approved) { CardPaymentInfo = "❌ " + res.Message; await _dialogService.ShowErrorAsync(res.Message); return; }

            CardAmount = res.Amount;                 // SyncMixedPaid → PaidAmount = نقد+کارت
            Reference = $"ترکیبی نقد:{CashAmount:N0} کارت:{CardAmount:N0} نسیه:{MixedCreditAmount:N0} RRN:{res.Rrn}";
            CardPaymentInfo = $"✔ سهمِ کارت تأیید شد · پایانه {res.TerminalId} · پیگیری {res.TraceNo} · کارت {res.MaskedPan} · RRN {res.Rrn}";
        }, "در حال ارتباط با کارت‌خوان...");
    }

    /// <summary>OPT-5: بارگذاریِ موجودیِ انبارِ انتخابی (productId→qty). 🏛️ کلاینت→API، دسکتاپ→Application.</summary>
    private async Task LoadStockForWarehouseAsync()
    {
        try
        {
            if (SelectedWarehouseId <= 0) { _onHand = new(); return; }
            var rows = !string.IsNullOrWhiteSpace(_api.BaseUrl)
                ? (await _api.GetWarehouseStockAsync(SelectedWarehouseId)).Select(s => (s.ProductId, s.Quantity))
                : (await _mediator.Send(new GetWarehouseStockQuery(SelectedWarehouseId))).Select(s => (s.ProductId, s.Quantity));
            _onHand = rows.GroupBy(s => s.ProductId).ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity));
        }
        catch { _onHand = new(); }
    }

    private decimal OnHandOf(int productId) => _onHand.TryGetValue(productId, out var q) ? q : 0;

    // در حینِ LoadAsync، ستِ SelectedWarehouseId نباید بارگذاریِ موازیِ موجودی (fire-and-forget)
    // را تریگر کند — وگرنه با کوئری‌های همان DbContext تداخل می‌کند («A second operation…»).
    private bool _suppressStockReload;
    partial void OnSelectedWarehouseIdChanged(int value) { if (!_suppressStockReload) _ = ReloadStockAsync(); }
    private async Task ReloadStockAsync()
    {
        await LoadStockForWarehouseAsync();
        foreach (var row in InvoiceItems) row.StockOnHand = OnHandOf(row.ProductId);
        if (SelectedProductItem != null) EntryOnHand = OnHandOf(SelectedProductItem.Id);
    }

    // UX-SALES-1 — هینتِ تاریخچهٔ قیمت برای کالای نوارِ ورود (آخرین قیمتِ فروش).
    [ObservableProperty] private string? _entryPriceHint;

    partial void OnSelectedProductItemChanged(ProductSearchResult? value)
    {
        EntryOnHand = value != null ? OnHandOf(value.Id) : 0;
        // با انتخابِ کالا، «فی» و «مالیات ٪» نوارِ ورود از خودِ کالا پر شود.
        if (value != null) { AddUnitPrice = value.Price; AddTaxPct = value.TaxRate; }
        _ = LoadEntryPriceHintAsync(value?.Id ?? 0);
    }

    /// <summary>آخرین قیمتِ فروشِ کالا (کلی + به همین مشتری) را برای هینت می‌خوانَد.</summary>
    private async Task LoadEntryPriceHintAsync(int productId)
    {
        if (productId <= 0) { EntryPriceHint = null; return; }
        try
        {
            var dto = await _mediator.Send(new SamaHesab.Application.Sales.Queries.GetProductLastPriceQuery(
                productId, SelectedCustomerId > 0 ? SelectedCustomerId : (int?)null));
            if (dto.LastPrice is null) { EntryPriceHint = "بدونِ سابقهٔ فروش"; return; }
            var s = $"آخرین فروش: {dto.LastPrice:N0}";
            if (!string.IsNullOrEmpty(dto.LastDate)) s += $" ({dto.LastDate})";
            if (dto.LastPriceForCustomer is decimal pc)
                s += $" · به این مشتری: {pc:N0}" + (string.IsNullOrEmpty(dto.LastDateForCustomer) ? "" : $" ({dto.LastDateForCustomer})");
            EntryPriceHint = s;
        }
        catch { EntryPriceHint = null; }
    }

    private PrintDocumentData BuildPrintData()
    {
        var customerName = Customers.FirstOrDefault(c => c.Id == SelectedCustomerId)?.Name ?? "—";
        // فقط ردیف‌های واقعی (ردیف‌های خالیِ seed‌شده چاپ نشوند) + شمارهٔ ردیفِ پیوسته
        var lines = InvoiceItems.Where(i => i.ProductId > 0 && i.Quantity > 0)
            .Select((i, idx) => new PrintLine(
                idx + 1, i.ProductCode, i.ProductName, i.Quantity, i.UnitPrice, i.DiscountAmount, i.NetAmount)).ToList();
        // عنوانِ چاپ متناسب با نوعِ فاکتور (فروش/برگشت/پیش‌فاکتور).
        var docTitle = IsReturnInvoice ? "برگشت از فروش" : IsQuotationInvoice ? "پیش‌فاکتور" : "فاکتور فروش";
        return new PrintDocumentData(docTitle, InvoiceNumber, InvoiceDate, "مشتری", customerName,
            lines, SubTotal, TotalDiscount + InvoiceDiscount, TotalTax, Shipping, GrandTotal, PaidAmount, RemainAmount, Description);
    }

    public override async Task LoadAsync()
    {
        InvoiceDate = _calendar.GetCurrentPersianDate();
        var online = !string.IsNullOrWhiteSpace(_api.BaseUrl);

        // 🏛️ کلاینت→API، دسکتاپ→Application
        Customers = online
            ? (await _api.GetCustomersAsync()).Select(c => new CustomerItem(c.Id, c.Name, c.Mobile)).ToList()
            : (await _mediator.Send(new GetCustomersQuery())).Select(c => new CustomerItem(c.Id, c.Name, c.Mobile)).ToList();
        OnPropertyChanged(nameof(Customers));

        Warehouses = online
            ? (await _api.GetWarehousesAsync()).Select(w => new WarehouseItem(w.Id, w.Name)).ToList()
            : (await _mediator.Send(new GetWarehousesQuery())).Select(w => new WarehouseItem(w.Id, w.Name)).ToList();
        OnPropertyChanged(nameof(Warehouses));
        // ستِ انبارِ پیش‌فرض بدونِ تریگرِ بارگذاریِ موازی؛ سپس بارگذاریِ موجودی به‌صورتِ سریالی await می‌شود.
        _suppressStockReload = true;
        if (Warehouses.Any()) SelectedWarehouseId = Warehouses[0].Id;
        _suppressStockReload = false;
        await LoadStockForWarehouseAsync();

        AllProducts = online
            ? (await _api.GetProductListAsync()).Select(p => new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.SalePrice, p.TaxRate)).ToList()
            : (await _mediator.Send(new GetProductsQuery())).Select(p => new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.SalePrice, p.TaxRate)).ToList();
        OnPropertyChanged(nameof(AllProducts));

        try
        {
            var projects = await _mediator.Send(new SamaHesab.Application.Accounting.Dimensions.GetProjectsQuery(ActiveOnly: true));
            Projects = projects.Select(p => new ProjectItem(p.Id, p.Name)).ToList();
            OnPropertyChanged(nameof(Projects));
        }
        catch { /* نبودِ پروژه نباید فرم را خراب کند */ }

        try { _activeFiscalYearId = await _mediator.Send(new SamaHesab.Application.Accounting.Dimensions.GetActiveFiscalYearQuery()); }
        catch { /* fallback به ۱ اگر لود نشد */ }

        await LoadRecentCustomersAsync();

        // DT-3: قالب‌های چاپِ فاکتور فروش
        try
        {
            PrintTemplates.Clear();
            foreach (var t in await _mediator.Send(new GetDocumentTemplatesQuery("SalesInvoice"))) PrintTemplates.Add(t);
        }
        catch { /* نبودِ قالب نباید فرم را خراب کند */ }
    }

    /// <summary>ارقامِ لاتین → فارسی (برای مقادیرِ نمایشیِ قالبِ چاپ).</summary>
    private static string Fa(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s) sb.Append(c >= '0' && c <= '9' ? (char)('۰' + (c - '0')) : c);
        return sb.ToString();
    }

    /// <summary>DT-3 — چاپِ فاکتور با قالبِ انتخاب‌شده (موتورِ قالبِ پویا).</summary>
    [RelayCommand]
    private async Task PrintWithTemplateAsync(DocumentTemplateListDto? tpl)
    {
        if (tpl is null) return;
        if (!InvoiceItems.Any()) { await _dialogService.ShowWarningAsync("ردیفی برای چاپ نیست."); return; }
        try
        {
            var full = await _mediator.Send(new GetDocumentTemplateQuery(tpl.Id));
            if (full is null) { await _dialogService.ShowErrorAsync("قالب یافت نشد."); return; }

            var customerName = Customers.FirstOrDefault(c => c.Id == SelectedCustomerId)?.Name ?? "—";
            string N(decimal d) => Fa(d.ToString("N0"));
            var fields = new Dictionary<string, string?>
            {
                ["InvoiceNumber"] = Fa(InvoiceNumber), ["InvoiceDate"] = Fa(InvoiceDate),
                ["CustomerName"] = customerName, ["CustomerCode"] = Fa(SelectedCustomerId.ToString()),
                ["TotalAmount"] = N(GrandTotal), ["Tax"] = N(TotalTax),
                ["Discount"] = N(TotalDiscount + InvoiceDiscount), ["BranchName"] = "سما حساب",
                // L6 — QR/بارکد: payload خام (بدونِ تبدیلِ رقم) تا کدگذاری/base64 سالم بماند.
                ["DocNumber"] = InvoiceNumber, ["QrData"] = InvoiceNumber,
                ["QrImage"] = _barcode.QrImageHtml(InvoiceNumber, 60),
            };
            // فقط ردیف‌های واقعی (ردیف‌های خالیِ seed‌شده چاپ نشوند)
            var rows = InvoiceItems.Where(i => i.ProductId > 0 && i.Quantity > 0)
                .Select(i => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
            {
                ["ProductName"] = i.ProductName, ["ProductCode"] = i.ProductCode,
                ["Quantity"] = Fa(i.Quantity.ToString("0.##")), ["UnitPrice"] = N(i.UnitPrice),
                ["LineTotal"] = N(i.NetAmount),
            }).ToList();
            var data = DocumentData.Of(fields, rows);

            var html = DocumentTemplateEngine.Render(full.HeaderHtml, data)
                     + DocumentTemplateEngine.Render(full.BodyHtml, data)
                     + DocumentTemplateEngine.Render(full.FooterHtml, data);

            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "اسناد");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"فاکتور_{InvoiceNumber}_{tpl.Name}_{System.DateTime.Now:yyyyMMdd_HHmmss}.html");
            System.IO.File.WriteAllText(path, html, new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    /// <summary>کار #۳۹ — بارگذاری مشتریان اخیرِ کاربر جاری.</summary>
    private async Task LoadRecentCustomersAsync()
    {
        try
        {
            var recent = await _mediator.Send(new SamaHesab.Application.Common.Favorites.GetRecentItemsQuery("customer", 8));
            RecentCustomers.Clear();
            foreach (var r in recent) RecentCustomers.Add(new RecentRef(r.EntityId, r.Label));
        }
        catch { /* بی‌اهمیت: نوار اخیر اختیاری است */ }
    }

    [RelayCommand]
    private void SelectRecentCustomer(RecentRef? r) { if (r != null) SelectedCustomerId = r.Id; }

    /// <summary>Add the product picked from the dropdown.</summary>
    [RelayCommand]
    private async Task AddSelectedProductAsync()
    {
        if (SelectedProductItem != null) { await AddToCartAsync(SelectedProductItem); SelectedProductItem = null; }
    }

    /// <summary>Reload customer list (after a quick-add) and optionally select one.</summary>
    public async Task ReloadCustomersAsync(int? selectId)
    {
        Customers = !string.IsNullOrWhiteSpace(_api.BaseUrl)
            ? (await _api.GetCustomersAsync()).Select(c => new CustomerItem(c.Id, c.Name, c.Mobile)).ToList()
            : (await _mediator.Send(new GetCustomersQuery())).Select(c => new CustomerItem(c.Id, c.Name, c.Mobile)).ToList();
        OnPropertyChanged(nameof(Customers));
        if (selectId.HasValue) SelectedCustomerId = selectId.Value;
    }

    [RelayCommand]
    private async Task SearchProductAsync()
    {
        if (string.IsNullOrWhiteSpace(ProductSearch)) return;
        var products = !string.IsNullOrWhiteSpace(_api.BaseUrl)
            ? (await _api.GetProductListAsync(ProductSearch)).Select(p => new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.SalePrice, p.TaxRate)).ToList()
            : (await _mediator.Send(new GetProductsQuery(ProductSearch))).Select(p => new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.SalePrice, p.TaxRate)).ToList();
        SearchResults.Clear();
        foreach (var p in products.Take(20)) SearchResults.Add(p);
        if (SearchResults.Count == 1) { await AddToCartAsync(SearchResults[0]); ProductSearch = string.Empty; }
    }

    [RelayCommand]
    private async Task AddToCartAsync(ProductSearchResult? product)
    {
        if (product == null) return;
        // اگر کاربر «فی»/«مالیات» را دستی وارد/ویرایش کرده، حفظ شود؛ وگرنه از خودِ کالا پر شود
        // (مسیرِ بارکد/جستجو که نوارِ ورود را پر نمی‌کند).
        if (AddUnitPrice <= 0) AddUnitPrice = product.Price;
        if (AddTaxPct <= 0) AddTaxPct = product.TaxRate;
        var existing = InvoiceItems.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing != null)
        {
            existing.Quantity += AddQty;
            // U6: تخفیفِ پلکانیِ مقداری برای مقدارِ جدید (اگر تخفیفِ دستی روی ردیف نباشد)
            if (existing.DiscountPct <= 0)
            {
                var d = await ResolveQtyDiscountAsync(product.Id, existing.Quantity);
                if (d > 0) existing.DiscountPct = d;
            }
            existing.Recalculate();
        }
        else
        {
            var disc = AddDiscountPct;
            if (disc <= 0) disc = await ResolveQtyDiscountAsync(product.Id, AddQty);   // U6
            var row = new SalesInvoiceItemRow
            {
                RowNumber = InvoiceItems.Count + 1, ProductId = product.Id,
                ProductCode = product.Code, ProductName = product.Name,
                Quantity = AddQty, UnitPrice = AddUnitPrice,
                DiscountPct = disc, TaxPct = AddTaxPct,
                StockOnHand = OnHandOf(product.Id)
            };
            row.Recalculate(); row.PropertyChanged += (_, _) => RecalculateTotals();
            InvoiceItems.Add(row);
        }
        RecalculateTotals();
        ProductSearch = string.Empty; AddQty = 1; AddDiscountPct = 0; AddUnitPrice = 0; AddTaxPct = 0; SearchResults.Clear();
        RowAdded?.Invoke();   // T10 — بازگشتِ فوکوس به نوارِ ورود برای ردیفِ بعدی
        // کار #۳۹: ثبت استفاده‌ی کالا برای «کالاهای پرتکرار»
        _ = _mediator.Send(new SamaHesab.Application.Common.Favorites.TouchRecentItemCommand("product", product.Id, product.Name));
    }

    /// <summary>U6: بهترین تخفیفِ پلکانیِ مقداری برای (کالا، مقدار)؛ خطا/نبودِ پله → ۰.</summary>
    private async Task<decimal> ResolveQtyDiscountAsync(int productId, decimal qty)
    {
        try { return await _mediator.Send(new SamaHesab.Application.Inventory.DiscountTiers.ResolveQtyDiscountQuery(productId, qty)); }
        catch { return 0; }
    }

    /// <summary>ورود سریع: اسکن/تایپ بارکد یا کد کالا + Enter → ردیف فوراً افزوده می‌شود (سرویس یکپارچهٔ بارکد #۲۷). فوکوس برای ورود پیوسته حفظ می‌شود.</summary>
    [RelayCommand]
    private async Task ProcessBarcodeAsync()
    {
        if (string.IsNullOrWhiteSpace(BarcodeInput)) return;
        var code = BarcodeInput.Trim();
        var hit = await _mediator.Send(new SamaHesab.Application.Common.Barcode.ResolveBarcodeQuery(code));
        if (hit == null)
        {
            // اگر بارکد نبود، یک جستجوی نام انجام بده (🏛️ کلاینت→API، دسکتاپ→Application)
            var found = !string.IsNullOrWhiteSpace(_api.BaseUrl)
                ? (await _api.GetProductListAsync(code)).Select(p => new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.SalePrice, p.TaxRate)).ToList()
                : (await _mediator.Send(new GetProductsQuery(code))).Select(p => new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.SalePrice, p.TaxRate)).ToList();
            if (found.Count == 1)
            {
                await AddToCartAsync(found[0]);
                BarcodeInput = string.Empty;
                return;
            }
            // U-PUR-1/سیستمی: قبلاً وقتی چند کالا با این عبارت مچ می‌شد، پیامِ گمراه‌کنندهٔ
            // «یافت نشد» نشان داده می‌شد و کاربر هیچ راهی برایِ انتخاب نداشت. حالا نتایج زیرِ
            // فیلد نمایش داده می‌شوند (همان الگوی فاکتورِ خرید).
            if (found.Count > 1)
            {
                SearchResults.Clear();
                foreach (var p in found.Take(20)) SearchResults.Add(p);
                return;
            }
            await _dialogService.ShowWarningAsync($"کالا با کد «{code}» یافت نشد.");
            BarcodeInput = string.Empty;
            return;
        }
        await AddToCartAsync(new ProductSearchResult(hit.ProductId, hit.Code, hit.Name, code, hit.SalePrice, hit.TaxRate));
        BarcodeInput = string.Empty;
    }

    /// <summary>دکمه‌های پنل تسویه: تعیین روش پرداخت (نقد/کارت‌خوان/چک/نسیه).</summary>
    [RelayCommand] private void SetPay(string? method) { if (!string.IsNullOrEmpty(method)) PaymentType = method!; }

    [RelayCommand] private void RemoveItem(SalesInvoiceItemRow? i) { if (i != null) { InvoiceItems.Remove(i); RenumberRows(); RecalculateTotals(); } }

    /// <summary>CC-5 — تکرارِ ردیف (راست‌کلیک): کالا/فی/تخفیف/مالیاتِ ردیف را در ردیفِ بعد کپی می‌کند.</summary>
    [RelayCommand]
    private void DuplicateRow(SalesInvoiceItemRow? i)
    {
        if (i == null || i.ProductId <= 0) return;
        var row = new SalesInvoiceItemRow
        {
            ProductId = i.ProductId, ProductCode = i.ProductCode, ProductName = i.ProductName,
            Unit = i.Unit, Quantity = i.Quantity, UnitPrice = i.UnitPrice,
            DiscountPct = i.DiscountPct, TaxPct = i.TaxPct, Description = i.Description,
            StockOnHand = i.StockOnHand
        };
        row.Recalculate();
        row.PropertyChanged += (_, _) => RecalculateTotals();
        InvoiceItems.Insert(InvoiceItems.IndexOf(i) + 1, row);
        RenumberRows();
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        SubTotal = InvoiceItems.Sum(i => i.Quantity * i.UnitPrice);
        TotalDiscount = InvoiceItems.Sum(i => i.DiscountAmount);
        TotalTax = InvoiceItems.Sum(i => i.TaxAmount);
        TotalQuantity = InvoiceItems.Where(i => i.ProductId > 0).Sum(i => i.Quantity);
        GrandTotal = SubTotal - TotalDiscount - InvoiceDiscount + TotalTax + Shipping + OtherCosts;
        if (GrandTotal < 0) GrandTotal = 0;
        RemainAmount = GrandTotal - PaidAmount;
        OnPropertyChanged(nameof(MixedCreditAmount));   // POS-IR-3 — نسیه با تغییرِ مبلغِ کل به‌روز شود
    }

    /// <summary>افزودنِ ردیفِ خالیِ قابلِ‌ویرایش در گرید (سبکِ کلاسیک).</summary>
    [RelayCommand]
    private void AddEmptyRow()
    {
        var row = new SalesInvoiceItemRow { RowNumber = InvoiceItems.Count + 1, Quantity = 1, Unit = "عدد" };
        row.PropertyChanged += (_, _) => RecalculateTotals();
        InvoiceItems.Add(row);
        RenumberRows();
    }


    private void RenumberRows()
    { for (int i = 0; i < InvoiceItems.Count; i++) InvoiceItems[i].RowNumber = i + 1; }

    partial void OnInvoiceDiscountChanged(decimal value) => RecalculateTotals();

    [RelayCommand]
    private async Task PostInvoiceAsync()
    {
        // 👁 UX-SALES-VIEW — فاکتورِ ثبت‌شده فقط مشاهده/چاپ است؛ ثبتِ دوباره سندِ تکراری می‌سازد.
        if (IsViewingExisting)
        {
            await _dialogService.ShowWarningAsync("این فاکتور قبلاً ثبت شده و فقط برای مشاهده/چاپ باز شده است. برای فروشِ جدید، «فاکتورِ جدید (F2)» را بزنید.");
            return;
        }
        if (SelectedCustomerId == 0) { await _dialogService.ShowErrorAsync("مشتری را انتخاب کنید."); return; }
        var realItems = InvoiceItems.Where(i => i.ProductId > 0 && i.Quantity > 0).ToList();
        if (realItems.Count == 0) { await _dialogService.ShowErrorAsync("حداقل یک ردیفِ دارای کالا وارد کنید."); return; }

        var isReturn = InvoiceType == "برگشت از فروش";
        var isQuote = InvoiceType == "پیش‌فاکتور";

        // 🛡 کنترلِ سقفِ اعتبارِ مشتری: سهمِ نسیهٔ این فاکتور (نپرداخته) به ماندهٔ بدهی افزوده می‌شود.
        // سقف۰ = نامحدود؛ فقط وقتی اطلاعاتِ اعتبار بارگذاری شده و سقف معنادار است کنترل می‌کنیم.
        // در مرجوعی/پیش‌فاکتور کنترلِ سقف لازم نیست (مرجوعی بدهی را کم می‌کند؛ پیش‌فاکتور اثرِ مالی ندارد).
        var creditPortion = RemainAmount > 0 ? RemainAmount : 0;
        if (!isReturn && !isQuote && HasCustomerInfo && !CustomerUnlimitedCredit && CustomerCreditLimit > 0 && creditPortion > 0)
        {
            var projected = CustomerBalance + creditPortion;
            if (projected > CustomerCreditLimit)
            {
                var over = projected - CustomerCreditLimit;
                var pass = await _dialogService.ConfirmAsync(
                    $"⚠ این فاکتور از سقفِ اعتبارِ مشتری عبور می‌کند.\n\n" +
                    $"ماندهٔ بدهیِ فعلی: {CustomerBalance:N0} ریال\n" +
                    $"نسیهٔ این فاکتور: {creditPortion:N0} ریال\n" +
                    $"ماندهٔ پس از ثبت: {projected:N0} ریال\n" +
                    $"سقفِ اعتبار: {CustomerCreditLimit:N0} ریال\n" +
                    $"مازاد بر سقف: {over:N0} ریال\n\n" +
                    $"با مسئولیتِ خود ادامه می‌دهید؟");
                if (!pass) return;
            }
        }

        // 🔁 مسیرِ مرجوعی (برگشت از فروش): به CreateSalesReturnCommand می‌رود نه فروشِ معمول.
        if (isReturn)
        {
            await PostSalesReturnAsync(realItems);
            return;
        }

        var ok = await _dialogService.ConfirmAsync(isQuote
            ? $"پیش‌فاکتور به مبلغِ {GrandTotal:N0} ریال ثبت شود؟ (بدونِ خروجِ موجودی و سندِ مالی)"
            : $"فاکتور فروش {GrandTotal:N0} ریال قطعی شود؟");
        if (!ok) return;
        // POS-IR-3 — در پرداختِ ترکیبی اگر ارجاع خالی است، تفکیکِ نقد/کارت/نسیه را ثبت کن.
        if (IsMixedPayment && string.IsNullOrWhiteSpace(Reference))
            Reference = $"ترکیبی نقد:{CashAmount:N0} کارت:{CardAmount:N0} نسیه:{MixedCreditAmount:N0}";
        await ExecuteAsync(async () =>
        {
                        var cmd = new CreateSalesInvoiceCommand(
                BranchId: _currentUser.BranchId ?? 1, FiscalYearId: _activeFiscalYearId,
                InvoiceDate: InvoiceDate, CustomerId: SelectedCustomerId,
                WarehouseId: SelectedWarehouseId,
                InvoiceType: isQuote ? Domain.Enums.InvoiceType.Quotation : Domain.Enums.InvoiceType.Sale,
                PriceLevel: PriceLevel,
                SalesRepId: CommissionPercent > 0 ? (_currentUser.UserId ?? 1) : (int?)null,
                DueDate: DueDate, Description: Description,
                Shipping: Shipping, OtherCosts: OtherCosts,
                Items: realItems.Select(i => new SalesInvoiceItemDto(
                    i.ProductId, i.Quantity, i.UnitPrice, i.DiscountPct, i.TaxPct,
                    string.IsNullOrWhiteSpace(i.Description) ? null : i.Description, null, null)).ToList(),
                InvoiceDiscount: InvoiceDiscount,
                PaidAmount: isQuote ? 0 : PaidAmount,   // پیش‌فاکتور پرداختی ثبت نمی‌کند
                PaymentMethod: PaymentType,
                CommissionPercent: CommissionPercent,
                Reference: string.IsNullOrWhiteSpace(Reference) ? null : Reference,
                Title: string.IsNullOrWhiteSpace(Title) ? null : Title,
                ProjectId: SelectedProjectId);
            var result = await _mediator.Send(cmd);
            if (result.Succeeded) { await _dialogService.ShowSuccessAsync(isQuote ? "پیش‌فاکتور ثبت شد." : "فاکتور فروش ثبت شد."); NewInvoice(); }
            else await _dialogService.ShowErrorAsync(result.ErrorMessage);
        }, isQuote ? "در حال ثبت پیش‌فاکتور..." : "در حال ثبت فاکتور...");
    }

    /// <summary>ثبتِ «برگشت از فروش»: بازگشتِ موجودی + سندِ معکوس. بازپرداختِ نقدی اگر روشِ پرداخت نقدی باشد.</summary>
    private async Task PostSalesReturnAsync(List<SalesInvoiceItemRow> realItems)
    {
        var refundCash = PaymentType is "نقدی" or "کارتخوان";
        var ok = await _dialogService.ConfirmAsync(
            $"برگشت از فروش به مبلغِ {GrandTotal:N0} ریال ثبت شود؟\n" +
            (refundCash ? "(بازپرداختِ نقدی)" : "(کاهشِ ماندهٔ بدهیِ مشتری)"));
        if (!ok) return;
        await ExecuteAsync(async () =>
        {
            var cmd = new CreateSalesReturnCommand(
                BranchId: _currentUser.BranchId ?? 1, FiscalYearId: _activeFiscalYearId,
                Date: InvoiceDate, CustomerId: SelectedCustomerId, WarehouseId: SelectedWarehouseId,
                Items: realItems.Select(i => new SalesReturnItemDto(
                    i.ProductId, i.Quantity, i.UnitPrice, i.TaxPct)).ToList(),
                Description: string.IsNullOrWhiteSpace(Description) ? "برگشت از فروش" : Description,
                RefundCash: refundCash);
            var result = await _mediator.Send(cmd);
            if (result.Succeeded) { await _dialogService.ShowSuccessAsync("برگشت از فروش ثبت شد."); NewInvoice(); }
            else await _dialogService.ShowErrorAsync(result.ErrorMessage);
        }, "در حال ثبت برگشت از فروش...");
    }

    [RelayCommand]
    private void NewInvoice()
    {
        AutoNumber = true;
        InvoiceNumber = "--- خودکار ---";
        InvoiceDate = _calendar.GetCurrentPersianDate();
        SelectedCustomerId = 0; SelectedCustomerName = string.Empty;
        Reference = string.Empty; Title = string.Empty; SelectedProjectId = null;
        Description = null; DueDate = null; InvoiceItems.Clear();
        PaidAmount = 0; CashAmount = 0; CardAmount = 0; CardPaymentInfo = null;
        IsViewingExisting = false;   // خروج از حالتِ مشاهده
        RecalculateTotals();
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (!InvoiceItems.Any()) { await _dialogService.ShowErrorAsync("فاکتور خالی است."); return; }
        try { _printService.PrintInvoice(BuildPrintData()); }
        catch (Exception ex) { await _dialogService.ShowErrorAsync("خطا در چاپ: " + ex.Message); }
    }

    [RelayCommand]
    private async Task PrintPreviewAsync()
    {
        if (!InvoiceItems.Any()) { await _dialogService.ShowErrorAsync("فاکتور خالی است."); return; }
        try { _printService.Preview(BuildPrintData()); }
        catch (Exception ex) { await _dialogService.ShowErrorAsync("خطا در پیش‌نمایش: " + ex.Message); }
    }
    [RelayCommand] private async Task SaveDraftAsync() { await _dialogService.ShowSuccessAsync("پیش‌نویس ذخیره شد."); }

    partial void OnShippingChanged(decimal value) => RecalculateTotals();
    partial void OnOtherCostsChanged(decimal value) => RecalculateTotals();
    partial void OnPaidAmountChanged(decimal value) => RemainAmount = GrandTotal - value;

    /// <summary>با انتخاب مشتری، وضعیت اعتبار (مانده/سقف) را از Application می‌خواند و نوار اطلاعات را پر می‌کند.</summary>
    partial void OnSelectedCustomerIdChanged(int value)
    {
        if (value <= 0) { HasCustomerInfo = false; RecentProducts.Clear(); return; }
        _ = LoadCustomerCreditAsync(value);
        _ = LoadRecentProductsAsync(value);   // UX-SALES-2
        // کار #۳۹: ثبت استفاده برای فهرست «مشتریان اخیر»
        var name = Customers.FirstOrDefault(c => c.Id == value)?.Name;
        if (!string.IsNullOrWhiteSpace(name))
            _ = _mediator.Send(new SamaHesab.Application.Common.Favorites.TouchRecentItemCommand("customer", value, name!));
    }

    // UX-SALES-2 — کالاهای اخیراً خریداری‌شدهٔ مشتری (چیپ‌های افزودنِ سریع / سفارشِ مجدد).
    public ObservableCollection<ProductSearchResult> RecentProducts { get; } = new();

    private async Task LoadRecentProductsAsync(int customerId)
    {
        try
        {
            RecentProducts.Clear();
            var items = await _mediator.Send(new SamaHesab.Application.Sales.Queries.GetCustomerRecentProductsQuery(customerId));
            foreach (var p in items)
                RecentProducts.Add(new ProductSearchResult(p.ProductId, p.Code, p.Name, p.Barcode, p.Price, p.TaxRate));
        }
        catch { /* چیپ‌های پیشنهادی نباید فرم را بشکنند */ }
    }

    /// <summary>کلیک روی چیپِ کالای اخیرِ مشتری → افزودنِ فوری به سبد.</summary>
    [RelayCommand]
    private async Task AddRecentProduct(ProductSearchResult? product)
    {
        if (product == null) return;
        await AddToCartAsync(product);
    }

    private async Task LoadCustomerCreditAsync(int customerId)
    {
        try
        {
            var dto = await _mediator.Send(new SamaHesab.Application.CRM.Queries.GetCustomerCreditQuery(customerId));
            if (dto is null) { HasCustomerInfo = false; return; }
            CustomerBalance = dto.Balance;
            CustomerCreditLimit = dto.CreditLimit;
            CustomerUnlimitedCredit = dto.CreditLimit <= 0;     // سقف۰ = نامحدود
            IsOverCredit = dto.IsOverLimit;
            HasCustomerInfo = true;
        }
        catch { HasCustomerInfo = false; }
    }

    /// <summary>دکمه‌های پنل تسویه: تعیین روش پرداخت (نقد/کارت‌خوان/چک/نسیه).</summary>
    [RelayCommand] private void SetPayment(string? method) { if (!string.IsNullOrEmpty(method)) PaymentType = method!; }
}

public partial class SalesInvoiceItemRow : ObservableObject
{
    [ObservableProperty] private int _rowNumber;
    [ObservableProperty] private int _productId;
    [ObservableProperty] private string _productCode = string.Empty;
    [ObservableProperty] private string _productName = string.Empty;
    [ObservableProperty] private string _unit = "عدد";
    [ObservableProperty] private string? _description;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private ProductSearchResult? _selectedProduct;

    /// <summary>انتخابِ کالا در گرید (سبکِ کلاسیک) → پر شدنِ خودکارِ کد/نام/فی/مالیات.</summary>
    partial void OnSelectedProductChanged(ProductSearchResult? value)
    {
        if (value == null) return;
        ProductId = value.Id;
        ProductCode = value.Code;
        ProductName = value.Name;
        if (UnitPrice <= 0) UnitPrice = value.Price;
        if (TaxPct <= 0) TaxPct = value.TaxRate;
        Recalculate();
    }
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _discountPct;
    [ObservableProperty] private decimal _taxPct;
    [ObservableProperty] private decimal _discountAmount;
    [ObservableProperty] private decimal _taxAmount;
    [ObservableProperty] private decimal _netAmount;
    [ObservableProperty] private decimal _stockOnHand;   // OPT-5: موجودیِ انبار

    /// <summary>OPT-5: کسریِ موجودی — مقدارِ درخواستی بیش از موجودیِ انبار است.</summary>
    public bool IsShort => StockOnHand < Quantity;
    partial void OnStockOnHandChanged(decimal value) => OnPropertyChanged(nameof(IsShort));

    partial void OnQuantityChanged(decimal value) { Recalculate(); OnPropertyChanged(nameof(IsShort)); }
    partial void OnUnitPriceChanged(decimal value) => Recalculate();
    partial void OnDiscountPctChanged(decimal value) => Recalculate();
    partial void OnTaxPctChanged(decimal value) => Recalculate();

    public void Recalculate()
    {
        var sub = Quantity * UnitPrice;
        DiscountAmount = sub * DiscountPct / 100;
        var after = sub - DiscountAmount;
        TaxAmount = after * TaxPct / 100;
        NetAmount = after + TaxAmount;
    }
}

/// <summary>پارامترِ ناوبری برایِ پیش‌انتخابِ مشتری در فاکتورِ جدید (مثلاً از دکمهٔ «فاکتورِ جدید»یِ کارتِ مشتری) — با شناسهٔ خامِ فاکتور (int) اشتباه گرفته نشود.</summary>
public record PreselectCustomerParam(int CustomerId);

public record RecentRef(int Id, string Label);
public record CustomerItem(int Id, string Name, string? Mobile);
public record WarehouseItem(int Id, string Name);
public record ProjectItem(int Id, string Name);
public record ProductSearchResult(int Id, string Code, string Name, string? Barcode, decimal Price, decimal TaxRate);

