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
    [ObservableProperty] private decimal _roundingAdjustment;   // 🇮🇷 POS-IR-1 — اختلافِ گرد کردن
    [ObservableProperty] private decimal _cashReceived;
    [ObservableProperty] private decimal _change;
    [ObservableProperty] private string _paymentMode = "نقدی";
    [ObservableProperty] private string _currentTime = string.Empty;
    [ObservableProperty] private string _receiptNumber = string.Empty;

    // T13 — حالتِ مرجوعی (برگشت از فروش): سبد به‌جای فروش، برگشت ثبت می‌کند (افزایشِ موجودی + بازپرداخت).
    [ObservableProperty] private bool _isReturnMode;
    public string CheckoutLabel => IsReturnMode ? "ثبت برگشت / بازپرداخت" : "پرداخت نقدی — Enter";
    public string ReturnToggleLabel => IsReturnMode ? "حالت فروش" : "مرجوعی / برگشت";
    partial void OnIsReturnModeChanged(bool value)
    { OnPropertyChanged(nameof(CheckoutLabel)); OnPropertyChanged(nameof(ReturnToggleLabel)); }
    [RelayCommand] private void ToggleReturnMode() => IsReturnMode = !IsReturnMode;

    public ObservableCollection<PosCartItem> CartItems { get; } = new();
    public List<string> PaymentModes { get; } = new() { "نقدی", "کارتخوان", "ترکیبی" };

    // U11 — فاکتورهای معلق (Hold/Recall، کار #۳۳). فهرستِ فاکتورهای پارک‌شده برای فراخوانِ بعدی.
    public ObservableCollection<HeldSaleRow> HeldSales { get; } = new();
    [ObservableProperty] private bool _hasHeldSales;

    // شبکه‌ی کالاهای لمسی + دسته‌بندی (طبق طرح pos.html)
    public ObservableCollection<PosCategoryTile> Categories { get; } = new();
    public ObservableCollection<PosProductTile> Products { get; } = new();
    [ObservableProperty] private int _selectedCategoryId = -1;
    [ObservableProperty] private string _quickSearch = string.Empty;
    private List<PosProductTile> _allProducts = new();

    // U9 — کالاهای محبوب/پرتکرار (سنجاق‌شده‌ها اول، سپس اخیرها) برای دسترسی سریع در صندوق.
    // دستهٔ مجازیِ id=-2 «⭐ محبوب» این فهرست را نمایش می‌دهد.
    private const int FavoriteCategoryId = -2;
    private List<int> _favoriteIds = new();

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

        await LoadFavoritesAsync();
        await LoadHeldSalesAsync();
        ApplyFilter();
    }

    /// <summary>U9 — بارگذاریِ کالاهای محبوب/اخیر؛ اگر آیتمی بود، دستهٔ «⭐ محبوب» به ابتدا افزوده می‌شود.</summary>
    private async Task LoadFavoritesAsync()
    {
        try
        {
            List<int> pinned, recent;
            if (UseApi)
            {
                pinned = (await _api.GetPinnedItemsAsync("product")).Select(i => i.EntityId).ToList();
                recent = (await _api.GetRecentItemsAsync("product", 16)).Select(i => i.EntityId).ToList();
            }
            else
            {
                pinned = (await _mediator.Send(new SamaHesab.Application.Common.Favorites.GetPinnedItemsQuery("product"))).Select(i => i.EntityId).ToList();
                recent = (await _mediator.Send(new SamaHesab.Application.Common.Favorites.GetRecentItemsQuery("product", 16))).Select(i => i.EntityId).ToList();
            }
            // سنجاق‌شده‌ها اول، سپس اخیرها — بدونِ تکرار، و فقط کالاهایی که واقعاً در فهرست هستند.
            var valid = _allProducts.Select(p => p.Id).ToHashSet();
            _favoriteIds = pinned.Concat(recent).Distinct().Where(valid.Contains).ToList();
            if (_favoriteIds.Count > 0 && Categories.All(c => c.Id != FavoriteCategoryId))
                Categories.Insert(1, new PosCategoryTile(FavoriteCategoryId, "⭐ محبوب"));
        }
        catch { /* بهره‌وری؛ نبودش نباید صندوق را خراب کند */ }
    }

    private void ApplyFilter()
    {
        Products.Clear();
        IEnumerable<PosProductTile> q;
        if (SelectedCategoryId == FavoriteCategoryId)
        {
            // ترتیبِ محبوب‌ها حفظ شود (سنجاق‌شده‌ها اول).
            var rank = _favoriteIds.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);
            q = _allProducts.Where(p => rank.ContainsKey(p.Id)).OrderBy(p => rank[p.Id]);
        }
        else
        {
            q = _allProducts;
            if (SelectedCategoryId != -1) q = q.Where(p => p.GroupId == SelectedCategoryId);
        }
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
        TouchProduct(tile.Id, tile.Name);   // U9 — ثبت استفاده تا فهرستِ «محبوب» به‌روز بماند
    }

    /// <summary>U9 — ثبتِ best-effortِ استفاده از کالا (به‌روزرسانیِ فهرستِ اخیر/محبوب). فروش را بلاک نمی‌کند.</summary>
    private void TouchProduct(int productId, string name)
    {
        if (UseApi) _ = _api.TouchRecentAsync("product", productId, name);
        else _ = _mediator.Send(new SamaHesab.Application.Common.Favorites.TouchRecentItemCommand("product", productId, name));
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
        TouchProduct(hit.Value.Id, hit.Value.Name);   // U9
        BarcodeInput = string.Empty;
    }

    [RelayCommand] private void RemoveItem(PosCartItem? item) { if (item != null) { CartItems.Remove(item); RecalculateTotals(); } }
    [RelayCommand] private void ClearCart() { CartItems.Clear(); RecalculateTotals(); }

    // ── U11 — تعلیق/فراخوانِ فاکتور (Hold/Recall، #۳۳) ──────────────────────────
    private record HeldLine(int ProductId, string Code, string Name, decimal Quantity, decimal UnitPrice, decimal TaxRate);
    private record HeldCart(List<HeldLine> Items, decimal Discount, int? CustomerId, string? CustomerName);

    private async Task LoadHeldSalesAsync()
    {
        try
        {
            List<HeldSaleRow> rows;
            if (UseApi)
                rows = (await _api.GetHeldSalesAsync()).Select(h => new HeldSaleRow(h.Id, h.Label, h.Total, h.CreatedAt)).ToList();
            else
                rows = (await _mediator.Send(new SamaHesab.Application.POS.GetHeldSalesQuery()))
                    .Select(h => new HeldSaleRow(h.Id, h.Label, h.Total, h.CreatedAt)).ToList();
            HeldSales.Clear();
            foreach (var r in rows) HeldSales.Add(r);
            HasHeldSales = HeldSales.Count > 0;
        }
        catch { /* بهره‌وری؛ نبودش نباید صندوق را خراب کند */ }
    }

    /// <summary>سبدِ فعلی را پارک می‌کند تا بعداً فراخوان شود (مثلاً مشتری چیزی جا گذاشته).</summary>
    [RelayCommand]
    private async Task HoldSaleAsync()
    {
        if (!CartItems.Any()) { await _dialogService.ShowWarningAsync("سبد خرید خالی است."); return; }
        var label = SelectedCustomerName is { Length: > 0 } cn
            ? cn
            : $"فاکتور {DateTime.Now:HH:mm} ({CartItems.Count} قلم)";
        var cart = new HeldCart(
            CartItems.Select(i => new HeldLine(i.ProductId, i.ProductCode, i.ProductName, i.Quantity, i.UnitPrice, i.TaxRate)).ToList(),
            Discount, SelectedCustomerId, SelectedCustomerName);
        var payload = System.Text.Json.JsonSerializer.Serialize(cart);

        bool ok; string? error;
        if (UseApi) { (ok, _, error) = await _api.HoldSaleAsync(label, payload, GrandTotal); }
        else
        {
            var r = await _mediator.Send(new SamaHesab.Application.POS.HoldSaleCommand(label, payload, GrandTotal));
            ok = r.Succeeded; error = r.ErrorMessage;
        }
        if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در تعلیق فاکتور."); return; }

        NewSale();
        await LoadHeldSalesAsync();
    }

    /// <summary>فاکتورِ معلق را به سبد بازمی‌گرداند و از فهرستِ معلق حذف می‌کند.</summary>
    [RelayCommand]
    private async Task RecallSaleAsync(HeldSaleRow? row)
    {
        if (row == null) return;
        if (CartItems.Any())
        {
            var ok = await _dialogService.ConfirmAsync("سبد فعلی خالی و با فاکتور معلق جایگزین شود؟");
            if (!ok) return;
        }

        SamaHesab.Application.POS.HeldSaleDetailDto? detail = null;
        string? payload = null;
        if (UseApi) { var d = await _api.GetHeldSaleAsync(row.Id); payload = d?.Payload; }
        else { detail = await _mediator.Send(new SamaHesab.Application.POS.GetHeldSaleQuery(row.Id)); payload = detail?.Payload; }
        if (string.IsNullOrWhiteSpace(payload)) { await _dialogService.ShowErrorAsync("فاکتور معلق یافت نشد."); await LoadHeldSalesAsync(); return; }

        HeldCart? cart;
        try { cart = System.Text.Json.JsonSerializer.Deserialize<HeldCart>(payload); }
        catch { cart = null; }
        if (cart == null) { await _dialogService.ShowErrorAsync("دادهٔ فاکتور معلق نامعتبر است."); return; }

        CartItems.Clear();
        foreach (var l in cart.Items)
        {
            var item = new PosCartItem(l.ProductId, l.Code, l.Name, l.Quantity, l.UnitPrice, l.TaxRate);
            item.PropertyChanged += (_, _) => RecalculateTotals();
            CartItems.Add(item);
        }
        Discount = cart.Discount;
        SelectedCustomerId = cart.CustomerId;
        SelectedCustomerName = cart.CustomerName;
        RecalculateTotals();

        // پس از فراخوان، رکوردِ معلق پاک می‌شود (یک‌بارمصرف).
        if (UseApi) await _api.DeleteHeldSaleAsync(row.Id);
        else await _mediator.Send(new SamaHesab.Application.POS.DeleteHeldSaleCommand(row.Id));
        await LoadHeldSalesAsync();
    }

    /// <summary>حذفِ یک فاکتور معلق بدونِ فراخوان.</summary>
    [RelayCommand]
    private async Task DeleteHeldAsync(HeldSaleRow? row)
    {
        if (row == null) return;
        if (!await _dialogService.ConfirmAsync($"فاکتور معلق «{row.Label}» حذف شود؟")) return;
        if (UseApi) await _api.DeleteHeldSaleAsync(row.Id);
        else await _mediator.Send(new SamaHesab.Application.POS.DeleteHeldSaleCommand(row.Id));
        await LoadHeldSalesAsync();
    }

    private void RecalculateTotals()
    {
        SubTotal   = CartItems.Sum(i => i.Quantity * i.UnitPrice);
        Tax        = CartItems.Sum(i => i.TaxAmount);
        var raw    = SubTotal - Discount + Tax;
        // 🇮🇷 POS-IR-1 — گرد کردنِ مبلغِ نهایی (تنظیم‌پذیر؛ ۰=خاموش).
        var step   = Services.AppSettingsStore.GetGeneral().PosRoundingStep;
        GrandTotal = SamaHesab.Application.Common.MoneyRounding.RoundTo(raw, step);
        RoundingAdjustment = GrandTotal - raw;
        Change     = Math.Max(0, CashReceived - GrandTotal);
    }

    partial void OnCashReceivedChanged(decimal value) => Change = Math.Max(0, value - GrandTotal);
    partial void OnDiscountChanged(decimal value) => RecalculateTotals();

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        if (!CartItems.Any()) { await _dialogService.ShowWarningAsync("سبد خرید خالی است."); return; }
        // مرجوعی فعلاً فقط در حالتِ محلی (دسکتاپِ یکپارچه) پشتیبانی می‌شود.
        if (IsReturnMode && UseApi) { await _dialogService.ShowWarningAsync("ثبتِ برگشت در حالتِ صندوقِ مستقل فعلاً پشتیبانی نمی‌شود."); return; }
        // در فروشِ نقدی، دریافتی باید کافی باشد؛ در مرجوعی این کنترل لازم نیست (بازپرداخت است).
        if (!IsReturnMode && PaymentMode == "نقدی" && CashReceived < GrandTotal) { await _dialogService.ShowErrorAsync("مبلغ دریافتی کافی نیست."); return; }

        await ExecuteAsync(async () =>
        {
            var method = PaymentMode == "کارتخوان" ? "بانک" : "نقدی";
            int invoiceId;

            if (IsReturnMode)
            {
                // برگشت از فروش: موجودی برمی‌گردد + سندِ معکوس + بازپرداختِ کاملِ مبلغ.
                var cmd = new CreateSalesInvoiceCommand(
                    BranchId: _currentUser.BranchId ?? 1, FiscalYearId: 1,
                    InvoiceDate: _calendar.GetCurrentPersianDate(),
                    CustomerId: SelectedCustomerId ?? 1,
                    WarehouseId: 1,
                    InvoiceType: SamaHesab.Domain.Enums.InvoiceType.SaleReturn,
                    PriceLevel: "خرده", SalesRepId: null, DueDate: null, Description: "برگشت از فروش صندوق (POS)",
                    Shipping: 0, OtherCosts: 0,
                    Items: CartItems.Select(i => new SalesInvoiceItemDto(
                        i.ProductId, i.Quantity, i.UnitPrice, 0, i.TaxRate, null, null, null)).ToList(),
                    InvoiceDiscount: Discount,
                    PaidAmount: GrandTotal,   // کلِ مبلغ بازپرداخت می‌شود
                    PaymentMethod: method);
                var result = await _mediator.Send(cmd);
                if (!result.Succeeded) { await _dialogService.ShowErrorAsync(result.ErrorMessage); return; }
                _lastInvoiceId = result.Value;
                await _dialogService.ShowSuccessAsync($"برگشت ثبت شد (فاکتور #{result.Value}).\nبازپرداخت: {GrandTotal:N0} ریال");
                NewSale();
                return;
            }

            var paid = PaymentMode == "نقدی" ? GrandTotal : CashReceived;

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
        }, IsReturnMode ? "در حال ثبت برگشت..." : "در حال صدور فاکتور...");
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
public record HeldSaleRow(int Id, string Label, decimal Total, DateTime CreatedAt);
