using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Documents;
using SamaHesab.Application.Purchase.Commands;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Purchase;

public partial class PurchaseInvoiceEditViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IProductRepository _productRepository;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private string _invoiceNumber = "--- خودکار ---";
    [ObservableProperty] private string _invoiceDate = string.Empty;
    [ObservableProperty] private int _selectedSupplierId;
    [ObservableProperty] private int _selectedWarehouseId;
    [ObservableProperty] private string _invoiceType = "خرید";
    [ObservableProperty] private string? _dueDate;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string _productSearch = string.Empty;
    [ObservableProperty] private decimal _addQty = 1;
    [ObservableProperty] private decimal _addUnitPrice;
    [ObservableProperty] private decimal _addDiscountPct;
    [ObservableProperty] private decimal _addTaxPct;
    [ObservableProperty] private string? _addBatchNumber;
    [ObservableProperty] private string? _addProductionDate;
    [ObservableProperty] private string? _addExpiryDate;
    [ObservableProperty] private PurchaseInvoiceItemRow? _selectedItem;
    [ObservableProperty] private ProductSearchResult? _selectedProductItem;
    [ObservableProperty] private decimal _subTotal;
    [ObservableProperty] private decimal _totalDiscount;
    [ObservableProperty] private decimal _totalTax;
    [ObservableProperty] private decimal _shipping;
    [ObservableProperty] private decimal _otherCosts;
    [ObservableProperty] private decimal _grandTotal;
    [ObservableProperty] private decimal _paidAmount;
    [ObservableProperty] private decimal _remainAmount;
    [ObservableProperty] private string _paymentType = "نقدی";

    // OPT-6: ماندهٔ تأمین‌کنندهٔ انتخابی + موجودیِ کالای نوارِ ورود
    [ObservableProperty] private decimal _supplierBalance;
    [ObservableProperty] private bool _hasSupplierInfo;
    [ObservableProperty] private decimal _entryOnHand;

    /// <summary>T10 — پس از افزودنِ هر ردیف، View نوارِ ورود را دوباره فوکوس می‌کند (ورودِ پیوسته).</summary>
    public event System.Action? RowAdded;

    public ObservableCollection<PurchaseInvoiceItemRow> InvoiceItems { get; } = new();
    public ObservableCollection<ProductSearchResult> SearchResults { get; } = new();
    // L3 (DT-3 قرینهٔ خرید) — قالب‌های چاپِ فاکتور خرید/برگشت برای منوی «چاپ ▼».
    public ObservableCollection<DocumentTemplateListDto> PrintTemplates { get; } = new();
    public List<ProductSearchResult> AllProducts { get; private set; } = new();
    public List<SupplierItem> Suppliers { get; private set; } = new();
    public List<WarehouseItem> Warehouses { get; private set; } = new();
    public List<string> InvoiceTypes { get; } = new() { "خرید", "برگشت از خرید" };
    public List<string> PaymentTypes { get; } = new() { "نقدی", "کارتخوان", "چک", "نسیه" };

    private readonly IRepository<SamaHesab.Domain.Entities.CRM.Supplier> _supplierRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IStockItemRepository _stockRepository;
    private Dictionary<int, decimal> _onHand = new();

    public PurchaseInvoiceEditViewModel(IMediator mediator, ICurrentUserService currentUser,
        IProductRepository productRepository,
        IRepository<SamaHesab.Domain.Entities.CRM.Supplier> supplierRepository,
        IWarehouseRepository warehouseRepository,
        IStockItemRepository stockRepository,
        IDialogService dialogService,
        INavigationService navigationService, IPersianCalendarService calendar)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _currentUser = currentUser;
        _productRepository = productRepository; _calendar = calendar;
        _supplierRepository = supplierRepository; _warehouseRepository = warehouseRepository;
        _stockRepository = stockRepository;
    }

    private decimal OnHandOf(int productId) => _onHand.TryGetValue(productId, out var q) ? q : 0;

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

    // در حینِ LoadAsync، ستِ SelectedWarehouseId نباید بارگذاریِ موازیِ موجودی را تریگر کند
    // (تداخلِ DbContext: «A second operation…»). مثلِ SalesInvoiceEditViewModel.
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

    partial void OnSelectedSupplierIdChanged(int value)
    {
        var s = Suppliers.FirstOrDefault(x => x.Id == value);
        HasSupplierInfo = s != null;
        SupplierBalance = s?.Balance ?? 0;
    }

    public override async Task LoadAsync()
    {
        InvoiceDate = _calendar.GetCurrentPersianDate();
        var companyId = _currentUser.CompanyId ?? 1;

        var suppliers = await _supplierRepository.FindAsync(s => s.CompanyId == companyId && s.IsActive);
        Suppliers = suppliers.Select(s => new SupplierItem(s.Id, s.FullName, s.Mobile ?? "", s.Balance)).ToList();
        OnPropertyChanged(nameof(Suppliers));

        var warehouses = await _warehouseRepository.GetByCompanyAsync(companyId);
        Warehouses = warehouses.Select(w => new WarehouseItem(w.Id, w.Name)).ToList();
        OnPropertyChanged(nameof(Warehouses));
        _suppressStockReload = true;
        if (Warehouses.Any()) SelectedWarehouseId = Warehouses[0].Id;
        _suppressStockReload = false;
        await LoadStockForWarehouseAsync();

        var products = await _productRepository.SearchAsync(companyId, "");
        AllProducts = products.Select(p => new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.PurchasePrice, p.TaxRate)).ToList();
        OnPropertyChanged(nameof(AllProducts));

        await LoadPrintTemplatesAsync();
    }

    /// <summary>L3 — نوعِ قالب بر اساسِ نوعِ فاکتور (خرید/برگشت از خرید).</summary>
    private string TemplateDocType => InvoiceType == "برگشت از خرید" ? "PurchaseReturn" : "PurchaseInvoice";

    partial void OnInvoiceTypeChanged(string value) => _ = LoadPrintTemplatesAsync();

    private async Task LoadPrintTemplatesAsync()
    {
        try
        {
            PrintTemplates.Clear();
            foreach (var t in await _mediator.Send(new GetDocumentTemplatesQuery(TemplateDocType))) PrintTemplates.Add(t);
        }
        catch { /* نبودِ قالب نباید فرم را خراب کند */ }
    }

    [RelayCommand]
    private void AddSelectedProduct()
    {
        if (SelectedProductItem == null) { _ = _dialogService.ShowErrorAsync("کالا را انتخاب کنید."); return; }
        AddProduct(SelectedProductItem);
        SelectedProductItem = null;
    }

    [RelayCommand]
    private async Task SearchProductAsync()
    {
        if (string.IsNullOrWhiteSpace(ProductSearch)) return;
        var products = await _productRepository.SearchAsync(_currentUser.CompanyId!.Value, ProductSearch);
        SearchResults.Clear();
        foreach (var p in products.Take(20))
            SearchResults.Add(new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.PurchasePrice, p.TaxRate));
        if (products.Count == 1) { AddProduct(SearchResults[0]); ProductSearch = string.Empty; }
    }

    [RelayCommand]
    private void AddProduct(ProductSearchResult? product)
    {
        if (product == null) return;
        AddUnitPrice = product.Price; AddTaxPct = product.TaxRate;
        var row = new PurchaseInvoiceItemRow
        {
            RowNumber = InvoiceItems.Count + 1, ProductId = product.Id,
            ProductCode = product.Code, ProductName = product.Name,
            Quantity = AddQty, UnitPrice = AddUnitPrice,
            DiscountPct = AddDiscountPct, TaxPct = AddTaxPct,
            BatchNumber = AddBatchNumber, ProductionDate = AddProductionDate, ExpiryDate = AddExpiryDate,
            StockOnHand = OnHandOf(product.Id)
        };
        row.Recalculate();
        row.PropertyChanged += (_, _) => RecalculateTotals();
        InvoiceItems.Add(row);
        RecalculateTotals();
        ProductSearch = string.Empty; AddQty = 1;
        AddBatchNumber = null; AddProductionDate = null; AddExpiryDate = null;
        SearchResults.Clear();
        RowAdded?.Invoke();   // T10 — بازگشتِ فوکوس به نوارِ ورود برای ردیفِ بعدی
    }

    [RelayCommand] private void RemoveItem(PurchaseInvoiceItemRow? item) { if (item != null) { InvoiceItems.Remove(item); RecalculateTotals(); } }
    [RelayCommand] private void ClearItems() { InvoiceItems.Clear(); RecalculateTotals(); }

    private void RecalculateTotals()
    {
        SubTotal = InvoiceItems.Sum(i => i.Quantity * i.UnitPrice);
        TotalDiscount = InvoiceItems.Sum(i => i.DiscountAmount);
        TotalTax = InvoiceItems.Sum(i => i.TaxAmount);
        GrandTotal = SubTotal - TotalDiscount + TotalTax + Shipping + OtherCosts;
        RemainAmount = GrandTotal - PaidAmount;
    }

    [RelayCommand]
    private async Task PostInvoiceAsync()
    {
        if (SelectedSupplierId == 0) { await _dialogService.ShowErrorAsync("تأمین‌کننده انتخاب کنید."); return; }
        if (!InvoiceItems.Any()) { await _dialogService.ShowErrorAsync("حداقل یک ردیف وارد کنید."); return; }
        var confirm = await _dialogService.ConfirmAsync($"فاکتور خرید {GrandTotal:N0} ریال قطعی شود؟");
        if (!confirm) return;
        await ExecuteAsync(async () =>
        {
            var cmd = new CreatePurchaseInvoiceCommand(
                _currentUser.BranchId ?? 1, 1, InvoiceDate, SelectedSupplierId,
                SelectedWarehouseId, InvoiceType, null, DueDate, Description, Shipping, OtherCosts,
                InvoiceItems.Select(i => new PurchaseInvoiceItemDto(
                    i.ProductId, i.Quantity, i.UnitPrice, i.DiscountPct, i.TaxPct,
                    i.Description, null, i.BatchNumber, i.ProductionDate, i.ExpiryDate)).ToList(),
                PaidAmount: PaidAmount);
            var result = await _mediator.Send(cmd);
            if (result.Succeeded)
            {
                await _dialogService.ShowSuccessAsync("فاکتور خرید ثبت شد و موجودی بروزرسانی گردید.");
                NewInvoice();
            }
            else await _dialogService.ShowErrorAsync(result.ErrorMessage);
        }, "در حال ثبت...");
    }

    [RelayCommand]
    private void NewInvoice()
    {
        InvoiceNumber = "--- خودکار ---";
        InvoiceDate = _calendar.GetCurrentPersianDate();
        SelectedSupplierId = 0; Description = null; DueDate = null;
        InvoiceItems.Clear(); PaidAmount = 0; RecalculateTotals();
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (!InvoiceItems.Any()) { await _dialogService.ShowWarningAsync("ردیفی برای چاپ نیست."); return; }
        try
        {
            string N(decimal d) => d.ToString("N0");
            var supplier = Suppliers.FirstOrDefault(s => s.Id == SelectedSupplierId)?.Name ?? "—";
            var rows = InvoiceItems.Select(i => new[] {
                i.RowNumber.ToString(), i.ProductCode, i.ProductName, N(i.Quantity), N(i.UnitPrice),
                N(i.DiscountAmount), N(i.TaxAmount), N(i.NetAmount) }).ToList();
            rows.Add(new[] { "", "", "جمع کل", "", "", N(TotalDiscount), N(TotalTax), N(GrandTotal) });
            var table = new ReportTable(
                $"فاکتور خرید {InvoiceNumber} — {supplier} — {InvoiceDate}",
                new[] { "ردیف", "کد", "نام کالا", "مقدار", "فی", "تخفیف", "مالیات", "مبلغ خالص" }, rows);
            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "اسناد");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"فاکتور_خرید_{InvoiceNumber}_{System.DateTime.Now:yyyyMMdd_HHmmss}.html");
            System.IO.File.WriteAllText(path, ReportExporter.ToHtml(table), new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    /// <summary>L3 (DT-3 قرینهٔ خرید) — چاپِ فاکتور خرید/برگشت با قالبِ انتخاب‌شده (موتورِ قالبِ پویا).</summary>
    [RelayCommand]
    private async Task PrintWithTemplateAsync(DocumentTemplateListDto? tpl)
    {
        if (tpl is null) return;
        if (!InvoiceItems.Any()) { await _dialogService.ShowWarningAsync("ردیفی برای چاپ نیست."); return; }
        try
        {
            var full = await _mediator.Send(new GetDocumentTemplateQuery(tpl.Id));
            if (full is null) { await _dialogService.ShowErrorAsync("قالب یافت نشد."); return; }

            var supplier = Suppliers.FirstOrDefault(s => s.Id == SelectedSupplierId)?.Name ?? "—";
            string N(decimal d) => d.ToString("N0");
            var fields = new Dictionary<string, string?>
            {
                ["InvoiceNumber"] = InvoiceNumber, ["DocNumber"] = InvoiceNumber, ["InvoiceDate"] = InvoiceDate,
                ["SupplierName"] = supplier, ["SupplierCode"] = SelectedSupplierId.ToString(),
                ["TotalAmount"] = N(GrandTotal), ["GrandTotal"] = N(GrandTotal), ["SubTotal"] = N(SubTotal),
                ["Tax"] = N(TotalTax), ["Discount"] = N(TotalDiscount), ["BranchName"] = "سما حساب",
                ["WarehouseName"] = Warehouses.FirstOrDefault(w => w.Id == SelectedWarehouseId)?.Name ?? "—",
                ["Notes"] = Description,
            };
            var rows = InvoiceItems.Select(i => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
            {
                ["ProductName"] = i.ProductName, ["ProductCode"] = i.ProductCode,
                ["Quantity"] = i.Quantity.ToString("0.##"), ["UnitPrice"] = N(i.UnitPrice),
                ["LineDiscount"] = N(i.DiscountAmount), ["LineTax"] = N(i.TaxAmount), ["LineTotal"] = N(i.NetAmount),
            }).ToList();
            var data = DocumentData.Of(fields, rows);

            var html = DocumentTemplateEngine.Render(full.HeaderHtml, data)
                     + DocumentTemplateEngine.Render(full.BodyHtml, data)
                     + DocumentTemplateEngine.Render(full.FooterHtml, data);

            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "اسناد");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"خرید_{InvoiceNumber}_{tpl.Name}_{System.DateTime.Now:yyyyMMdd_HHmmss}.html");
            System.IO.File.WriteAllText(path, html, new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }
}

public partial class PurchaseInvoiceItemRow : ObservableObject
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
    [ObservableProperty] private string? _batchNumber;
    [ObservableProperty] private string? _productionDate;
    [ObservableProperty] private string? _expiryDate;
    [ObservableProperty] private decimal _discountAmount;
    [ObservableProperty] private decimal _taxAmount;
    [ObservableProperty] private decimal _netAmount;
    [ObservableProperty] private decimal _stockOnHand;   // OPT-6: موجودیِ انبار

    partial void OnQuantityChanged(decimal value) => Recalculate();
    partial void OnUnitPriceChanged(decimal value) => Recalculate();
    partial void OnDiscountPctChanged(decimal value) => Recalculate();
    partial void OnTaxPctChanged(decimal value) => Recalculate();

    public void Recalculate()
    {
        var sub = Quantity * UnitPrice;
        DiscountAmount = sub * DiscountPct / 100;
        var afterDiscount = sub - DiscountAmount;
        TaxAmount = afterDiscount * TaxPct / 100;
        NetAmount = afterDiscount + TaxAmount;
    }
}

public record SupplierItem(int Id, string Name, string? Mobile, decimal Balance = 0);
public record WarehouseItem(int Id, string Name);
public record ProductSearchResult(int Id, string Code, string Name, string? Barcode, decimal Price, decimal TaxRate);
