using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Application.Documents;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.Application.Purchase.Commands;
using SamaHesab.Application.Reports.Export;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Purchase;

/// <summary>ثبتِ فاکتور خرید — 🏛️ الگوی API-only: کلاینت→API، دسکتاپ→Application. بدونِ ریپازیتوریِ مستقیم.</summary>
public partial class PurchaseInvoiceEditViewModel : BaseViewModel, SamaHesab.WPF.Services.INavigationAware
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly ApiClient _api;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private string _invoiceNumber = "--- خودکار ---";
    [ObservableProperty] private bool _autoNumber = true;            // شمارهٔ خودکار (کلاسیک)
    [ObservableProperty] private decimal _totalQuantity;
    [ObservableProperty] private string _invoiceDate = string.Empty;
    [ObservableProperty] private int _selectedSupplierId;

    partial void OnAutoNumberChanged(bool value)
    { InvoiceNumber = value ? "--- خودکار ---" : (InvoiceNumber == "--- خودکار ---" ? string.Empty : InvoiceNumber); }
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

    // UX-PURCHASE-VIEW — حالتِ مشاهدهٔ فاکتورِ ثبت‌شده (قفلِ ثبتِ دوباره)
    [ObservableProperty] private bool _isViewingExisting;
    partial void OnIsViewingExistingChanged(bool value) => OnPropertyChanged(nameof(PostButtonText));
    public string PostButtonText => IsViewingExisting ? "👁 فاکتورِ ثبت‌شده — فقط مشاهده/چاپ"
                                                       : "✓ ثبت نهایی خرید — F9";

    public async Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is int id && id > 0) await LoadExistingAsync(id);
    }

    private async Task LoadExistingAsync(int id)
    {
        await ExecuteAsync(async () =>
        {
            var d = await _mediator.Send(new SamaHesab.Application.Purchase.Queries.GetPurchaseInvoiceByIdQuery(id));
            if (d == null) { await _dialogService.ShowErrorAsync("فاکتور یافت نشد."); return; }

            AutoNumber = false;
            InvoiceNumber = d.Number;
            InvoiceDate = d.Date;
            InvoiceType = d.InvoiceType;
            _suppressStockReload = true;
            SelectedWarehouseId = d.WarehouseId;
            _suppressStockReload = false;
            SelectedSupplierId = d.SupplierId;
            DueDate = d.DueDate;
            Description = d.Description;
            Shipping = d.Shipping;
            OtherCosts = d.OtherCosts;
            PaidAmount = d.PaidAmount;

            InvoiceItems.Clear();
            foreach (var it in d.Items)
            {
                var row = new PurchaseInvoiceItemRow
                {
                    RowNumber = InvoiceItems.Count + 1, ProductId = it.ProductId,
                    ProductCode = it.Code, ProductName = it.Name,
                    Quantity = it.Quantity, UnitPrice = it.UnitPrice,
                    DiscountPct = it.DiscountPct, TaxPct = it.TaxPct, Description = it.Description
                };
                row.Recalculate();
                row.PropertyChanged += (_, _) => RecalculateTotals();
                InvoiceItems.Add(row);
            }
            RecalculateTotals();
            IsViewingExisting = true;   // قفلِ ثبتِ دوباره
        }, "در حال بارگذاریِ فاکتور...");
    }
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

    private readonly IBarcodeService _barcode;   // L6 — تصویرِ QR برای چاپِ قالبی
    private readonly IPrintService _printService;
    private Dictionary<int, decimal> _onHand = new();

    public PurchaseInvoiceEditViewModel(IMediator mediator, ICurrentUserService currentUser,
        ApiClient api,
        IDialogService dialogService,
        INavigationService navigationService, IPersianCalendarService calendar,
        IBarcodeService barcode, IPrintService printService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _currentUser = currentUser;
        _api = api; _calendar = calendar; _barcode = barcode; _printService = printService;
    }

    private PrintDocumentData BuildPrintData()
    {
        var supplier = Suppliers.FirstOrDefault(s => s.Id == SelectedSupplierId)?.Name ?? "—";
        var lines = InvoiceItems.Where(i => i.ProductId > 0 && i.Quantity > 0)
            .Select((i, idx) => new PrintLine(
                idx + 1, i.ProductCode, i.ProductName, i.Quantity, i.UnitPrice, i.DiscountAmount, i.NetAmount)).ToList();
        return new PrintDocumentData("فاکتور خرید", InvoiceNumber, InvoiceDate, "تأمین‌کننده", supplier,
            lines, SubTotal, TotalDiscount, TotalTax, Shipping, GrandTotal, PaidAmount, RemainAmount, Description);
    }

    [RelayCommand]
    private async Task PrintPreviewAsync()
    {
        if (!InvoiceItems.Any(i => i.ProductId > 0)) { await _dialogService.ShowWarningAsync("ردیفی برای پیش‌نمایش نیست."); return; }
        try { _printService.Preview(BuildPrintData()); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync("خطا در پیش‌نمایش: " + ex.Message); }
    }

    private decimal OnHandOf(int productId) => _onHand.TryGetValue(productId, out var q) ? q : 0;

    private async Task LoadStockForWarehouseAsync()
    {
        try
        {
            if (SelectedWarehouseId <= 0) { _onHand = new(); return; }
            // 🏛️ کلاینت→API، دسکتاپ→Application
            var rows = !string.IsNullOrWhiteSpace(_api.BaseUrl)
                ? (await _api.GetWarehouseStockAsync(SelectedWarehouseId)).Select(s => (s.ProductId, s.Quantity))
                : (await _mediator.Send(new GetWarehouseStockQuery(SelectedWarehouseId))).Select(s => (s.ProductId, s.Quantity));
            _onHand = rows.GroupBy(s => s.ProductId).ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity));
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
    {
        EntryOnHand = value != null ? OnHandOf(value.Id) : 0;
        _ = LoadEntryPriceHintAsync(value?.Id ?? 0);   // UX-PURCHASE-1
    }

    partial void OnSelectedSupplierIdChanged(int value)
    {
        var s = Suppliers.FirstOrDefault(x => x.Id == value);
        HasSupplierInfo = s != null;
        SupplierBalance = s?.Balance ?? 0;
        _ = LoadRecentProductsAsync(value);   // UX-PURCHASE-1
    }

    // UX-PURCHASE-1 — تقارن با فروش: هینتِ آخرین قیمتِ خرید + خریدهای اخیر از تأمین‌کننده.
    [ObservableProperty] private string? _entryPriceHint;
    public ObservableCollection<ProductSearchResult> RecentProducts { get; } = new();

    private async Task LoadEntryPriceHintAsync(int productId)
    {
        if (productId <= 0) { EntryPriceHint = null; return; }
        try
        {
            var dto = await _mediator.Send(new SamaHesab.Application.Purchase.Queries.GetProductLastPurchasePriceQuery(
                productId, SelectedSupplierId > 0 ? SelectedSupplierId : (int?)null));
            if (dto.LastPrice is null) { EntryPriceHint = "بدونِ سابقهٔ خرید"; return; }
            var s = $"آخرین خرید: {dto.LastPrice:N0}";
            if (!string.IsNullOrEmpty(dto.LastDate)) s += $" ({dto.LastDate})";
            if (dto.LastPriceForSupplier is decimal ps)
                s += $" · از این تأمین‌کننده: {ps:N0}" + (string.IsNullOrEmpty(dto.LastDateForSupplier) ? "" : $" ({dto.LastDateForSupplier})");
            EntryPriceHint = s;
        }
        catch { EntryPriceHint = null; }
    }

    private async Task LoadRecentProductsAsync(int supplierId)
    {
        try
        {
            RecentProducts.Clear();
            if (supplierId <= 0) return;
            var items = await _mediator.Send(new SamaHesab.Application.Purchase.Queries.GetSupplierRecentProductsQuery(supplierId));
            foreach (var p in items)
                RecentProducts.Add(new ProductSearchResult(p.ProductId, p.Code, p.Name, p.Barcode, p.Price, p.TaxRate));
        }
        catch { /* چیپ‌های پیشنهادی نباید فرم را بشکنند */ }
    }

    /// <summary>کلیک روی چیپِ کالای اخیرِ تأمین‌کننده → افزودنِ فوریِ ردیف.</summary>
    [RelayCommand]
    private void AddRecentProduct(ProductSearchResult? product)
    {
        if (product == null) return;
        var existing = InvoiceItems.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing != null) { existing.Quantity += 1; existing.Recalculate(); RecalculateTotals(); return; }
        var row = new PurchaseInvoiceItemRow
        {
            ProductId = product.Id, ProductCode = product.Code, ProductName = product.Name,
            Unit = "عدد", Quantity = 1, UnitPrice = product.Price, TaxPct = product.TaxRate
        };
        row.Recalculate();
        row.PropertyChanged += (_, _) => RecalculateTotals();
        InvoiceItems.Add(row);
        RenumberRows();
        RecalculateTotals();
    }

    /// <summary>CC-5 — تکرارِ ردیفِ خرید (راست‌کلیک).</summary>
    [RelayCommand]
    private void DuplicateRow(PurchaseInvoiceItemRow? i)
    {
        if (i == null || i.ProductId <= 0) return;
        var row = new PurchaseInvoiceItemRow
        {
            ProductId = i.ProductId, ProductCode = i.ProductCode, ProductName = i.ProductName,
            Unit = i.Unit, Quantity = i.Quantity, UnitPrice = i.UnitPrice,
            DiscountPct = i.DiscountPct, TaxPct = i.TaxPct, Description = i.Description
        };
        row.Recalculate();
        row.PropertyChanged += (_, _) => RecalculateTotals();
        InvoiceItems.Insert(InvoiceItems.IndexOf(i) + 1, row);
        RenumberRows();
        RecalculateTotals();
    }

    public override async Task LoadAsync()
    {
        InvoiceDate = _calendar.GetCurrentPersianDate();
        var online = !string.IsNullOrWhiteSpace(_api.BaseUrl);

        // 🏛️ کلاینت→API، دسکتاپ→Application
        Suppliers = online
            ? (await _api.GetSuppliersAsync()).Select(s => new SupplierItem(s.Id, s.Name, s.Mobile, s.Balance)).ToList()
            : (await _mediator.Send(new GetSuppliersQuery())).Select(s => new SupplierItem(s.Id, s.Name, s.Mobile, s.Balance)).ToList();
        OnPropertyChanged(nameof(Suppliers));

        Warehouses = online
            ? (await _api.GetWarehousesAsync()).Select(w => new WarehouseItem(w.Id, w.Name)).ToList()
            : (await _mediator.Send(new GetWarehousesQuery())).Select(w => new WarehouseItem(w.Id, w.Name)).ToList();
        OnPropertyChanged(nameof(Warehouses));
        _suppressStockReload = true;
        if (Warehouses.Any()) SelectedWarehouseId = Warehouses[0].Id;
        _suppressStockReload = false;
        await LoadStockForWarehouseAsync();

        AllProducts = online
            ? (await _api.GetProductListAsync()).Select(p => new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.PurchasePrice, p.TaxRate)).ToList()
            : (await _mediator.Send(new GetProductsQuery())).Select(p => new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.PurchasePrice, p.TaxRate)).ToList();
        OnPropertyChanged(nameof(AllProducts));

        if (InvoiceItems.Count == 0) SeedEmptyRows();
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
        // 🏛️ کلاینت→API، دسکتاپ→Application
        var products = !string.IsNullOrWhiteSpace(_api.BaseUrl)
            ? (await _api.GetProductListAsync(ProductSearch)).Select(p => new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.PurchasePrice, p.TaxRate)).ToList()
            : (await _mediator.Send(new GetProductsQuery(ProductSearch))).Select(p => new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.PurchasePrice, p.TaxRate)).ToList();
        SearchResults.Clear();
        foreach (var p in products.Take(20)) SearchResults.Add(p);
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

    [RelayCommand] private void RemoveItem(PurchaseInvoiceItemRow? item) { if (item != null) { InvoiceItems.Remove(item); RenumberRows(); RecalculateTotals(); } }
    [RelayCommand] private void ClearItems() { InvoiceItems.Clear(); RecalculateTotals(); }

    /// <summary>افزودنِ ردیفِ خالیِ قابلِ‌ویرایش در گرید (سبکِ کلاسیک).</summary>
    [RelayCommand]
    private void AddEmptyRow()
    {
        var row = new PurchaseInvoiceItemRow { RowNumber = InvoiceItems.Count + 1, Quantity = 1, Unit = "عدد" };
        row.PropertyChanged += (_, _) => RecalculateTotals();
        InvoiceItems.Add(row); RenumberRows();
    }

    private void SeedEmptyRows(int count = 5)
    {
        for (int i = 0; i < count; i++)
        {
            var row = new PurchaseInvoiceItemRow { RowNumber = InvoiceItems.Count + 1, Quantity = 1, Unit = "عدد" };
            row.PropertyChanged += (_, _) => RecalculateTotals();
            InvoiceItems.Add(row);
        }
    }

    private void RenumberRows() { for (int i = 0; i < InvoiceItems.Count; i++) InvoiceItems[i].RowNumber = i + 1; }

    private void RecalculateTotals()
    {
        SubTotal = InvoiceItems.Sum(i => i.Quantity * i.UnitPrice);
        TotalDiscount = InvoiceItems.Sum(i => i.DiscountAmount);
        TotalTax = InvoiceItems.Sum(i => i.TaxAmount);
        TotalQuantity = InvoiceItems.Where(i => i.ProductId > 0).Sum(i => i.Quantity);
        GrandTotal = SubTotal - TotalDiscount + TotalTax + Shipping + OtherCosts;
        RemainAmount = GrandTotal - PaidAmount;
    }

    [RelayCommand]
    private async Task PostInvoiceAsync()
    {
        if (IsViewingExisting)
        {
            await _dialogService.ShowInfoAsync("این فاکتور قبلاً ثبت شده است؛ فقط قابلِ مشاهده/چاپ است. برای ثبتِ فاکتورِ تازه «فاکتور جدید» را بزنید.");
            return;
        }
        if (SelectedSupplierId == 0) { await _dialogService.ShowErrorAsync("تأمین‌کننده انتخاب کنید."); return; }
        var realItems = InvoiceItems.Where(i => i.ProductId > 0 && i.Quantity > 0).ToList();
        if (realItems.Count == 0) { await _dialogService.ShowErrorAsync("حداقل یک ردیفِ دارای کالا وارد کنید."); return; }
        var confirm = await _dialogService.ConfirmAsync($"فاکتور خرید {GrandTotal:N0} ریال قطعی شود؟");
        if (!confirm) return;
        await ExecuteAsync(async () =>
        {
            var cmd = new CreatePurchaseInvoiceCommand(
                _currentUser.BranchId ?? 1, 1, InvoiceDate, SelectedSupplierId,
                SelectedWarehouseId, InvoiceType, null, DueDate, Description, Shipping, OtherCosts,
                realItems.Select(i => new PurchaseInvoiceItemDto(
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
        AutoNumber = true;
        InvoiceNumber = "--- خودکار ---";
        InvoiceDate = _calendar.GetCurrentPersianDate();
        SelectedSupplierId = 0; Description = null; DueDate = null;
        IsViewingExisting = false;   // خروج از حالتِ مشاهده
        InvoiceItems.Clear(); SeedEmptyRows(); PaidAmount = 0; RecalculateTotals();
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

    /// <summary>ارقامِ لاتین → فارسی (برای مقادیرِ نمایشیِ قالبِ چاپ).</summary>
    private static string Fa(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s) sb.Append(c >= '0' && c <= '9' ? (char)('۰' + (c - '0')) : c);
        return sb.ToString();
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
            string N(decimal d) => Fa(d.ToString("N0"));
            var fields = new Dictionary<string, string?>
            {
                ["InvoiceNumber"] = Fa(InvoiceNumber), ["DocNumber"] = InvoiceNumber, ["InvoiceDate"] = Fa(InvoiceDate),
                ["SupplierName"] = supplier, ["SupplierCode"] = Fa(SelectedSupplierId.ToString()),
                ["TotalAmount"] = N(GrandTotal), ["GrandTotal"] = N(GrandTotal), ["SubTotal"] = N(SubTotal),
                ["Tax"] = N(TotalTax), ["Discount"] = N(TotalDiscount), ["BranchName"] = "سما حساب",
                ["WarehouseName"] = Warehouses.FirstOrDefault(w => w.Id == SelectedWarehouseId)?.Name ?? "—",
                ["Notes"] = Description,
                // L6 — QR/بارکد: payload خام (بدونِ تبدیلِ رقم) تا کدگذاری/base64 سالم بماند.
                ["QrData"] = InvoiceNumber, ["QrImage"] = _barcode.QrImageHtml(InvoiceNumber, 60),
            };
            // فقط ردیف‌های واقعی (ردیف‌های خالیِ seed‌شده چاپ نشوند)
            var rows = InvoiceItems.Where(i => i.ProductId > 0 && i.Quantity > 0)
                .Select(i => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
            {
                ["ProductName"] = i.ProductName, ["ProductCode"] = i.ProductCode,
                ["Quantity"] = Fa(i.Quantity.ToString("0.##")), ["UnitPrice"] = N(i.UnitPrice),
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
    [ObservableProperty] private string _unit = "عدد";
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
    [ObservableProperty] private ProductSearchResult? _selectedProduct;

    /// <summary>انتخابِ کالا در گرید (سبکِ کلاسیک) → پر شدنِ خودکارِ کد/نام/قیمتِ خرید/مالیات.</summary>
    partial void OnSelectedProductChanged(ProductSearchResult? value)
    {
        if (value == null) return;
        ProductId = value.Id; ProductCode = value.Code; ProductName = value.Name;
        if (UnitPrice <= 0) UnitPrice = value.Price;
        if (TaxPct <= 0) TaxPct = value.TaxRate;
        Recalculate();
    }

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
