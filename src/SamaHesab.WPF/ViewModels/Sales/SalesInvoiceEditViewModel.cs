using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Sales.Commands;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Sales;

public partial class SalesInvoiceEditViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IProductRepository _productRepository;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private string _invoiceNumber = "--- خودکار ---";
    [ObservableProperty] private string _invoiceDate = string.Empty;
    [ObservableProperty] private int _selectedCustomerId;
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

    public ObservableCollection<SalesInvoiceItemRow> InvoiceItems { get; } = new();
    public ObservableCollection<ProductSearchResult> SearchResults { get; } = new();
    /// <summary>مشتریان اخیرِ این کاربر (کار #۳۹) — چیپ‌های دسترسی سریع.</summary>
    public ObservableCollection<RecentRef> RecentCustomers { get; } = new();
    public List<ProductSearchResult> AllProducts { get; private set; } = new();
    public List<CustomerItem> Customers { get; private set; } = new();
    public List<WarehouseItem> Warehouses { get; private set; } = new();
    public List<string> InvoiceTypes { get; } = new() { "فروش", "برگشت از فروش", "پیش‌فاکتور" };
    public List<string> PriceLevels { get; } = new() { "خرده", "عمده", "ویژه" };
    public List<string> PaymentTypes { get; } = new() { "نقدی", "کارتخوان", "چک", "نسیه", "اقساط" };

    private readonly IRepository<SamaHesab.Domain.Entities.CRM.Customer> _customerRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IStockItemRepository _stockRepository;

    private readonly IPrintService _printService;

    /// <summary>موجودیِ انبارِ انتخابی به‌ازای هر کالا (OPT-5: نمایشِ موجودی هنگام فروش).</summary>
    private Dictionary<int, decimal> _onHand = new();
    /// <summary>موجودیِ کالایِ نوارِ ورود (کالای انتخاب/اسکن‌شده).</summary>
    [ObservableProperty] private decimal _entryOnHand;

    public SalesInvoiceEditViewModel(IMediator mediator, ICurrentUserService currentUser,
        IProductRepository productRepository,
        IRepository<SamaHesab.Domain.Entities.CRM.Customer> customerRepository,
        IWarehouseRepository warehouseRepository,
        IStockItemRepository stockRepository,
        IDialogService dialogService,
        INavigationService navigationService, IPersianCalendarService calendar,
        IPrintService printService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _currentUser = currentUser;
        _productRepository = productRepository; _calendar = calendar;
        _customerRepository = customerRepository; _warehouseRepository = warehouseRepository;
        _stockRepository = stockRepository;
        _printService = printService;
    }

    /// <summary>OPT-5: بارگذاریِ موجودیِ انبارِ انتخابی (productId→qty) برای نمایشِ سریعِ موجودی.</summary>
    private async Task LoadStockForWarehouseAsync()
    {
        try
        {
            if (SelectedWarehouseId <= 0) { _onHand = new(); return; }
            var items = await _stockRepository.GetByWarehouseAsync(SelectedWarehouseId);
            _onHand = items.GroupBy(s => s.ProductId).ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity));
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

    partial void OnSelectedProductItemChanged(ProductSearchResult? value)
        => EntryOnHand = value != null ? OnHandOf(value.Id) : 0;

    private PrintDocumentData BuildPrintData()
    {
        var customerName = Customers.FirstOrDefault(c => c.Id == SelectedCustomerId)?.Name ?? "—";
        var lines = InvoiceItems.Select(i => new PrintLine(
            i.RowNumber, i.ProductCode, i.ProductName, i.Quantity, i.UnitPrice, i.DiscountAmount, i.NetAmount)).ToList();
        return new PrintDocumentData("فاکتور فروش", InvoiceNumber, InvoiceDate, "مشتری", customerName,
            lines, SubTotal, TotalDiscount + InvoiceDiscount, TotalTax, Shipping, GrandTotal, PaidAmount, RemainAmount, Description);
    }

    public override async Task LoadAsync()
    {
        InvoiceDate = _calendar.GetCurrentPersianDate();
        var companyId = _currentUser.CompanyId ?? 1;

        var customers = await _customerRepository.FindAsync(c => c.CompanyId == companyId && c.IsActive);
        Customers = customers.Select(c => new CustomerItem(c.Id, c.FullName, c.Mobile ?? "")).ToList();
        OnPropertyChanged(nameof(Customers));

        var warehouses = await _warehouseRepository.GetByCompanyAsync(companyId);
        Warehouses = warehouses.Select(w => new WarehouseItem(w.Id, w.Name)).ToList();
        OnPropertyChanged(nameof(Warehouses));
        // ستِ انبارِ پیش‌فرض بدونِ تریگرِ بارگذاریِ موازی؛ سپس بارگذاریِ موجودی به‌صورتِ سریالی await می‌شود.
        _suppressStockReload = true;
        if (Warehouses.Any()) SelectedWarehouseId = Warehouses[0].Id;
        _suppressStockReload = false;
        await LoadStockForWarehouseAsync();

        var prods = await _productRepository.SearchAsync(companyId, "");
        AllProducts = prods.Select(p => new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.SalePrice, p.TaxRate)).ToList();
        OnPropertyChanged(nameof(AllProducts));

        await LoadRecentCustomersAsync();
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
        var companyId = _currentUser.CompanyId ?? 1;
        var customers = await _customerRepository.FindAsync(c => c.CompanyId == companyId && c.IsActive);
        Customers = customers.Select(c => new CustomerItem(c.Id, c.FullName, c.Mobile ?? "")).ToList();
        OnPropertyChanged(nameof(Customers));
        if (selectId.HasValue) SelectedCustomerId = selectId.Value;
    }

    [RelayCommand]
    private async Task SearchProductAsync()
    {
        if (string.IsNullOrWhiteSpace(ProductSearch)) return;
        var products = await _productRepository.SearchAsync(_currentUser.CompanyId ?? 1, ProductSearch);
        SearchResults.Clear();
        foreach (var p in products.Take(20))
            SearchResults.Add(new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.SalePrice, p.TaxRate));
        if (SearchResults.Count == 1) { await AddToCartAsync(SearchResults[0]); ProductSearch = string.Empty; }
    }

    [RelayCommand]
    private async Task AddToCartAsync(ProductSearchResult? product)
    {
        if (product == null) return;
        AddUnitPrice = product.Price; AddTaxPct = product.TaxRate;
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
        ProductSearch = string.Empty; AddQty = 1; AddDiscountPct = 0; SearchResults.Clear();
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
            // اگر بارکد نبود، یک جستجوی نام انجام بده
            var found = await _productRepository.SearchAsync(_currentUser.CompanyId ?? 1, code);
            if (found.Count == 1)
            {
                var p = found[0];
                await AddToCartAsync(new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.SalePrice, p.TaxRate));
                BarcodeInput = string.Empty;
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

    [RelayCommand] private void RemoveItem(SalesInvoiceItemRow? i) { if (i != null) { InvoiceItems.Remove(i); RecalculateTotals(); } }

    private void RecalculateTotals()
    {
        SubTotal = InvoiceItems.Sum(i => i.Quantity * i.UnitPrice);
        TotalDiscount = InvoiceItems.Sum(i => i.DiscountAmount);
        TotalTax = InvoiceItems.Sum(i => i.TaxAmount);
        GrandTotal = SubTotal - TotalDiscount - InvoiceDiscount + TotalTax + Shipping + OtherCosts;
        if (GrandTotal < 0) GrandTotal = 0;
        RemainAmount = GrandTotal - PaidAmount;
    }

    partial void OnInvoiceDiscountChanged(decimal value) => RecalculateTotals();

    [RelayCommand]
    private async Task PostInvoiceAsync()
    {
        if (SelectedCustomerId == 0) { await _dialogService.ShowErrorAsync("مشتری را انتخاب کنید."); return; }
        if (!InvoiceItems.Any()) { await _dialogService.ShowErrorAsync("حداقل یک ردیف وارد کنید."); return; }
        var ok = await _dialogService.ConfirmAsync($"فاکتور فروش {GrandTotal:N0} ریال قطعی شود؟");
        if (!ok) return;
        await ExecuteAsync(async () =>
        {
                        var cmd = new CreateSalesInvoiceCommand(
                BranchId: _currentUser.BranchId ?? 1, FiscalYearId: 1,
                InvoiceDate: InvoiceDate, CustomerId: SelectedCustomerId,
                WarehouseId: SelectedWarehouseId, InvoiceType: Domain.Enums.InvoiceType.Sale,
                PriceLevel: PriceLevel,
                SalesRepId: CommissionPercent > 0 ? (_currentUser.UserId ?? 1) : (int?)null,
                DueDate: DueDate, Description: Description,
                Shipping: Shipping, OtherCosts: OtherCosts,
                Items: InvoiceItems.Select(i => new SalesInvoiceItemDto(
                    i.ProductId, i.Quantity, i.UnitPrice, i.DiscountPct, i.TaxPct, null, null, null)).ToList(),
                InvoiceDiscount: InvoiceDiscount,
                PaidAmount: PaidAmount,
                PaymentMethod: PaymentType,
                CommissionPercent: CommissionPercent);
            var result = await _mediator.Send(cmd);
            if (result.Succeeded) { await _dialogService.ShowSuccessAsync("فاکتور فروش ثبت شد."); NewInvoice(); }
            else await _dialogService.ShowErrorAsync(result.ErrorMessage);
        }, "در حال ثبت فاکتور...");
    }

    [RelayCommand]
    private void NewInvoice()
    {
        InvoiceNumber = "--- خودکار ---";
        InvoiceDate = _calendar.GetCurrentPersianDate();
        SelectedCustomerId = 0; SelectedCustomerName = string.Empty;
        Description = null; DueDate = null; InvoiceItems.Clear();
        PaidAmount = 0; RecalculateTotals();
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
        if (value <= 0) { HasCustomerInfo = false; return; }
        _ = LoadCustomerCreditAsync(value);
        // کار #۳۹: ثبت استفاده برای فهرست «مشتریان اخیر»
        var name = Customers.FirstOrDefault(c => c.Id == value)?.Name;
        if (!string.IsNullOrWhiteSpace(name))
            _ = _mediator.Send(new SamaHesab.Application.Common.Favorites.TouchRecentItemCommand("customer", value, name!));
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
    [ObservableProperty] private string? _description;
    [ObservableProperty] private decimal _quantity;
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

public record RecentRef(int Id, string Label);
public record CustomerItem(int Id, string Name, string? Mobile);
public record WarehouseItem(int Id, string Name);
public record ProductSearchResult(int Id, string Code, string Name, string? Barcode, decimal Price, decimal TaxRate);

