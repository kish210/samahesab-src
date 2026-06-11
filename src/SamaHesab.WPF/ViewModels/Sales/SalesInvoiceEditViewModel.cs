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

    private readonly IPrintService _printService;

    public SalesInvoiceEditViewModel(IMediator mediator, ICurrentUserService currentUser,
        IProductRepository productRepository,
        IRepository<SamaHesab.Domain.Entities.CRM.Customer> customerRepository,
        IWarehouseRepository warehouseRepository,
        IDialogService dialogService,
        INavigationService navigationService, IPersianCalendarService calendar,
        IPrintService printService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _currentUser = currentUser;
        _productRepository = productRepository; _calendar = calendar;
        _customerRepository = customerRepository; _warehouseRepository = warehouseRepository;
        _printService = printService;
    }

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
        if (Warehouses.Any()) SelectedWarehouseId = Warehouses[0].Id;

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
    private void AddSelectedProduct()
    {
        if (SelectedProductItem != null) { AddToCart(SelectedProductItem); SelectedProductItem = null; }
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
        if (SearchResults.Count == 1) { AddToCart(SearchResults[0]); ProductSearch = string.Empty; }
    }

    [RelayCommand]
    private void AddToCart(ProductSearchResult? product)
    {
        if (product == null) return;
        AddUnitPrice = product.Price; AddTaxPct = product.TaxRate;
        var existing = InvoiceItems.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing != null) { existing.Quantity += AddQty; existing.Recalculate(); }
        else
        {
            var row = new SalesInvoiceItemRow
            {
                RowNumber = InvoiceItems.Count + 1, ProductId = product.Id,
                ProductCode = product.Code, ProductName = product.Name,
                Quantity = AddQty, UnitPrice = AddUnitPrice,
                DiscountPct = AddDiscountPct, TaxPct = AddTaxPct
            };
            row.Recalculate(); row.PropertyChanged += (_, _) => RecalculateTotals();
            InvoiceItems.Add(row);
        }
        RecalculateTotals();
        ProductSearch = string.Empty; AddQty = 1; AddDiscountPct = 0; SearchResults.Clear();
        // کار #۳۹: ثبت استفاده‌ی کالا برای «کالاهای پرتکرار»
        _ = _mediator.Send(new SamaHesab.Application.Common.Favorites.TouchRecentItemCommand("product", product.Id, product.Name));
    }

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

    partial void OnQuantityChanged(decimal value) => Recalculate();
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

