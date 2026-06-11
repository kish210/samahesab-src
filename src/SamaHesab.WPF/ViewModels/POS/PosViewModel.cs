using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Sales.Commands;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.POS;

public partial class PosViewModel : BaseViewModel
{
    private readonly IProductRepository _productRepo;
    private readonly IRepository<SamaHesab.Domain.Entities.Inventory.ProductGroup> _groupRepo;
    private readonly IPersianCalendarService _calendar;
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;
    private readonly IPrintService _printService;
    private readonly ApiClient _api;
    private int _lastInvoiceId;

    /// <summary>When true (standalone pos.exe), all data goes through the Web API, not the DB.</summary>
    public bool UseApi { get; set; }
    private int _apiCustomerId = 1, _apiWarehouseId = 1;
    public void ConfigureApi(int customerId, int warehouseId) { UseApi = true; _apiCustomerId = customerId; _apiWarehouseId = warehouseId; }

    [ObservableProperty] private string _barcodeInput = string.Empty;
    [ObservableProperty] private string? _selectedCustomerName;
    [ObservableProperty] private int? _selectedCustomerId;
    [ObservableProperty] private decimal _subTotal;
    [ObservableProperty] private decimal _discount;
    [ObservableProperty] private decimal _tax;
    [ObservableProperty] private decimal _grandTotal;
    [ObservableProperty] private decimal _cashReceived;
    [ObservableProperty] private decimal _change;
    [ObservableProperty] private string _paymentMode = "نقدی";
    [ObservableProperty] private string _currentTime = string.Empty;
    [ObservableProperty] private string _receiptNumber = string.Empty;

    public ObservableCollection<PosCartItem> CartItems { get; } = new();
    public List<string> PaymentModes { get; } = new() { "نقدی", "کارتخوان", "ترکیبی" };

    // شبکه‌ی کالاهای لمسی + دسته‌بندی (طبق طرح pos.html)
    public ObservableCollection<PosCategoryTile> Categories { get; } = new();
    public ObservableCollection<PosProductTile> Products { get; } = new();
    [ObservableProperty] private int _selectedCategoryId = -1;
    [ObservableProperty] private string _quickSearch = string.Empty;
    private List<PosProductTile> _allProducts = new();

    private readonly System.Windows.Threading.DispatcherTimer _timer;

    public PosViewModel(IProductRepository productRepo,
        IRepository<SamaHesab.Domain.Entities.Inventory.ProductGroup> groupRepo,
        IPersianCalendarService calendar,
        ICurrentUserService currentUser, IMediator mediator, IPrintService printService,
        ApiClient api, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _productRepo = productRepo; _groupRepo = groupRepo; _calendar = calendar; _currentUser = currentUser;
        _mediator = mediator; _printService = printService; _api = api;
        _timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        _timer.Start();
    }

    public override async Task LoadAsync()
    {
        ReceiptNumber = "POS-" + DateTime.Now.ToString("yyyyMMddHHmm");
        CurrentTime = DateTime.Now.ToString("HH:mm:ss");

        Categories.Clear();
        Categories.Add(new PosCategoryTile(-1, "همه"));
        var companyId = _currentUser.CompanyId ?? 1;
        if (UseApi)
        {
            var prods = await _api.SearchProductsAsync("");
            _allProducts = prods.Select(p => new PosProductTile(p.Id, p.Code, p.Name, p.SalePrice, p.TaxRate, p.GroupId)).ToList();
            foreach (var g in await _api.GetGroupsAsync()) Categories.Add(new PosCategoryTile(g.Id, g.Name));
        }
        else
        {
            var products = await _productRepo.FindAsync(p => p.CompanyId == companyId && p.IsActive);
            _allProducts = products.Select(p => new PosProductTile(p.Id, p.Code, p.Name, p.SalePrice, p.TaxRate, p.GroupId)).ToList();
            try { foreach (var g in (await _groupRepo.FindAsync(g => g.CompanyId == companyId)).OrderBy(g => g.Code)) Categories.Add(new PosCategoryTile(g.Id, g.Name)); }
            catch { }
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Products.Clear();
        IEnumerable<PosProductTile> q = _allProducts;
        if (SelectedCategoryId != -1) q = q.Where(p => p.GroupId == SelectedCategoryId);
        if (!string.IsNullOrWhiteSpace(QuickSearch))
            q = q.Where(p => p.Name.Contains(QuickSearch) || p.Code.Contains(QuickSearch));
        foreach (var p in q.Take(60)) Products.Add(p);
    }

    partial void OnQuickSearchChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SelectCategory(PosCategoryTile? cat)
    {
        if (cat == null) return;
        SelectedCategoryId = cat.Id;
        ApplyFilter();
    }

    [RelayCommand]
    private void AddProduct(PosProductTile? tile)
    {
        if (tile == null) return;
        var existing = CartItems.FirstOrDefault(i => i.ProductId == tile.Id);
        if (existing != null) { existing.Quantity++; existing.Recalculate(); }
        else
        {
            var item = new PosCartItem(tile.Id, tile.Code, tile.Name, 1, tile.Price, tile.TaxRate);
            item.PropertyChanged += (_, _) => RecalculateTotals();
            CartItems.Add(item);
        }
        RecalculateTotals();
    }

    [RelayCommand] private void IncItem(PosCartItem? i) { if (i != null) { i.Quantity++; i.Recalculate(); RecalculateTotals(); } }
    [RelayCommand] private void DecItem(PosCartItem? i) { if (i != null) { if (i.Quantity > 1) { i.Quantity--; i.Recalculate(); } else CartItems.Remove(i); RecalculateTotals(); } }

    [RelayCommand]
    private async Task ProcessBarcodeAsync()
    {
        if (string.IsNullOrWhiteSpace(BarcodeInput)) return;
        var code = BarcodeInput.Trim();

        // کار #۲۷ — سرویس بارکد یکپارچه (نرمال‌سازی ارقام فارسی + بارکد وزنی + fallback به کد کالا).
        // standalone از Web API، حالت درون‌برنامه از همان Query در Application.
        (int Id, string Code, string Name, decimal Price, decimal Tax)? hit = null;
        if (UseApi)
        {
            var b = await _api.ResolveBarcodeAsync(code);
            if (b != null) hit = (b.ProductId, b.Code, b.Name, b.SalePrice, b.TaxRate);
        }
        else
        {
            var b = await _mediator.Send(new SamaHesab.Application.Common.Barcode.ResolveBarcodeQuery(code));
            if (b != null) hit = (b.ProductId, b.Code, b.Name, b.SalePrice, b.TaxRate);
        }

        if (hit == null) { await _dialogService.ShowWarningAsync($"کالا با کد '{code}' یافت نشد."); BarcodeInput = string.Empty; return; }

        var existing = CartItems.FirstOrDefault(i => i.ProductId == hit.Value.Id);
        if (existing != null) { existing.Quantity++; existing.Recalculate(); }
        else
        {
            var item = new PosCartItem(hit.Value.Id, hit.Value.Code, hit.Value.Name, 1, hit.Value.Price, hit.Value.Tax);
            item.PropertyChanged += (_, _) => RecalculateTotals();
            CartItems.Add(item);
        }
        RecalculateTotals();
        BarcodeInput = string.Empty;
    }

    [RelayCommand] private void RemoveItem(PosCartItem? item) { if (item != null) { CartItems.Remove(item); RecalculateTotals(); } }
    [RelayCommand] private void ClearCart() { CartItems.Clear(); RecalculateTotals(); }

    private void RecalculateTotals()
    {
        SubTotal   = CartItems.Sum(i => i.Quantity * i.UnitPrice);
        Tax        = CartItems.Sum(i => i.TaxAmount);
        GrandTotal = SubTotal - Discount + Tax;
        Change     = Math.Max(0, CashReceived - GrandTotal);
    }

    partial void OnCashReceivedChanged(decimal value) => Change = Math.Max(0, value - GrandTotal);
    partial void OnDiscountChanged(decimal value) => RecalculateTotals();

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        if (!CartItems.Any()) { await _dialogService.ShowWarningAsync("سبد خرید خالی است."); return; }
        if (PaymentMode == "نقدی" && CashReceived < GrandTotal) { await _dialogService.ShowErrorAsync("مبلغ دریافتی کافی نیست."); return; }

        await ExecuteAsync(async () =>
        {
            var paid = PaymentMode == "نقدی" ? GrandTotal : CashReceived;
            var method = PaymentMode == "کارتخوان" ? "بانک" : "نقدی";
            int invoiceId;

            if (UseApi)
            {
                // صدور فوری از طریق وب‌سرویس (کلاینت مستقل، بدون دسترسی مستقیم به دیتابیس)
                var (ok, id, error) = await _api.CreatePosSaleAsync(
                    CartItems.Select(i => (i.ProductId, i.Quantity, i.UnitPrice, 0m, i.TaxRate)),
                    paid, method, _apiCustomerId, _apiWarehouseId, Discount);
                if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در صدور فاکتور."); return; }
                invoiceId = id;
            }
            else
            {
                // صدور فوری فاکتور فروش (کاهش موجودی + سند خودکار) — حالت محلی
                var cmd = new CreateSalesInvoiceCommand(
                    BranchId: _currentUser.BranchId ?? 1, FiscalYearId: 1,
                    InvoiceDate: _calendar.GetCurrentPersianDate(),
                    CustomerId: SelectedCustomerId ?? 1,
                    WarehouseId: 1,
                    InvoiceType: SamaHesab.Domain.Enums.InvoiceType.Sale,
                    PriceLevel: "خرده", SalesRepId: null, DueDate: null, Description: "فروش صندوق (POS)",
                    Shipping: 0, OtherCosts: 0,
                    Items: CartItems.Select(i => new SalesInvoiceItemDto(
                        i.ProductId, i.Quantity, i.UnitPrice, 0, i.TaxRate, null, null, null)).ToList(),
                    InvoiceDiscount: Discount,
                    PaidAmount: paid,
                    PaymentMethod: method);
                var result = await _mediator.Send(cmd);
                if (!result.Succeeded) { await _dialogService.ShowErrorAsync(result.ErrorMessage); return; }
                invoiceId = result.Value;
            }

            _lastInvoiceId = invoiceId;
            try { _printService.PrintReceipt(BuildReceiptData()); } catch { /* چاپ اختیاری */ }
            await _dialogService.ShowSuccessAsync($"فروش ثبت شد (فاکتور #{invoiceId}).\nباقیمانده: {Change:N0} ریال");
            NewSale();
        }, "در حال صدور فاکتور...");
    }

    private PrintDocumentData BuildReceiptData()
    {
        var lines = CartItems.Select((i, idx) => new PrintLine(
            idx + 1, i.ProductCode, i.ProductName, i.Quantity, i.UnitPrice, 0, i.NetAmount)).ToList();
        return new PrintDocumentData("رسید فروش", ReceiptNumber, _calendar.GetCurrentPersianDate(),
            "صندوق", _currentUser.FullName ?? "صندوق‌دار", lines,
            SubTotal, Discount, Tax, 0, GrandTotal, CashReceived, Change, null);
    }

    [RelayCommand]
    private void NewSale()
    {
        CartItems.Clear(); CashReceived = 0; Discount = 0;
        RecalculateTotals();
        ReceiptNumber = "POS-" + DateTime.Now.ToString("yyyyMMddHHmm");
        BarcodeInput = string.Empty;
    }

    [RelayCommand]
    private async Task PrintReceiptAsync()
    {
        if (!CartItems.Any()) { await _dialogService.ShowWarningAsync("سبد خرید خالی است."); return; }
        try { _printService.PrintReceipt(BuildReceiptData()); }
        catch (Exception ex) { await _dialogService.ShowErrorAsync("خطا در چاپ رسید: " + ex.Message); }
    }
}

public partial class PosCartItem : ObservableObject
{
    public int ProductId { get; }
    public string ProductCode { get; }
    public string ProductName { get; }
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _taxRate;
    [ObservableProperty] private decimal _taxAmount;
    [ObservableProperty] private decimal _netAmount;

    public PosCartItem(int id, string code, string name, decimal qty, decimal price, decimal taxRate)
    { ProductId = id; ProductCode = code; ProductName = name; _quantity = qty; _unitPrice = price; _taxRate = taxRate; Recalculate(); }

    partial void OnQuantityChanged(decimal value) => Recalculate();
    partial void OnUnitPriceChanged(decimal value) => Recalculate();

    public void Recalculate()
    { var sub = Quantity * UnitPrice; TaxAmount = sub * TaxRate / 100; NetAmount = sub + TaxAmount; }
}

public record PosCategoryTile(int Id, string Name);
public record PosProductTile(int Id, string Code, string Name, decimal Price, decimal TaxRate, int? GroupId);
