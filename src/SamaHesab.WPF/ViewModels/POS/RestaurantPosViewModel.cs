using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Sales.Commands;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.POS;

public partial class RestaurantPosViewModel : BaseViewModel
{
    private readonly IProductRepository _productRepo;
    private readonly IRepository<SamaHesab.Domain.Entities.Inventory.ProductGroup> _groupRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;
    private readonly IMediator _mediator;
    private readonly IPrintService _printService;

    [ObservableProperty] private string _orderType = "سالن";          // سالن / بیرون / پیک
    [ObservableProperty] private int _tableNumber;
    [ObservableProperty] private int _people = 1;
    [ObservableProperty] private string? _customerPhone;
    [ObservableProperty] private string? _customerName;
    [ObservableProperty] private string? _customerAddress;
    [ObservableProperty] private string _quickSearch = string.Empty;

    [ObservableProperty] private int _selectedCategoryId = -1; // -1 = همه
    [ObservableProperty] private string _selectedCategoryName = "همه";

    [ObservableProperty] private decimal _subTotal;
    [ObservableProperty] private decimal _discount;
    [ObservableProperty] private bool _serviceEnabled = true;
    [ObservableProperty] private decimal _servicePct = 10;
    [ObservableProperty] private decimal _serviceAmount;
    [ObservableProperty] private bool _taxEnabled = true;
    [ObservableProperty] private decimal _taxAmount;
    [ObservableProperty] private decimal _tip;        // انعام
    [ObservableProperty] private decimal _grandTotal;
    [ObservableProperty] private decimal _cashPaid;
    [ObservableProperty] private decimal _posPaid;
    [ObservableProperty] private decimal _remain;
    [ObservableProperty] private string _orderNumber = string.Empty;
    [ObservableProperty] private string _currentTime = string.Empty;

    public ObservableCollection<CategoryTile> Categories { get; } = new();
    public ObservableCollection<MenuTile> MenuItems { get; } = new();
    public ObservableCollection<OrderLine> OrderLines { get; } = new();
    public List<string> OrderTypes { get; } = new() { "سالن", "بیرون", "پیک" };

    private List<MenuTile> _allMenu = new();
    private readonly System.Windows.Threading.DispatcherTimer _timer;

    public RestaurantPosViewModel(IProductRepository productRepo,
        IRepository<SamaHesab.Domain.Entities.Inventory.ProductGroup> groupRepo,
        ICurrentUserService currentUser, IPersianCalendarService calendar,
        IMediator mediator, IPrintService printService,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _productRepo = productRepo; _groupRepo = groupRepo; _currentUser = currentUser;
        _calendar = calendar; _mediator = mediator; _printService = printService;
        _timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        _timer.Start();
    }

    public override async Task LoadAsync()
    {
        var companyId = _currentUser.CompanyId ?? 1;
        OrderNumber = "R-" + DateTime.Now.ToString("yyMMddHHmm");
        CurrentTime = DateTime.Now.ToString("HH:mm:ss");

        var products = await _productRepo.FindAsync(p => p.CompanyId == companyId && p.IsActive);
        _allMenu = products.Select(p => new MenuTile(p.Id, p.Code, p.Name, p.SalePrice, p.GroupId, p.TaxRate)).ToList();

        Categories.Clear();
        Categories.Add(new CategoryTile(-1, "همه"));
        try
        {
            var groups = await _groupRepo.FindAsync(g => g.CompanyId == companyId);
            foreach (var g in groups.OrderBy(g => g.Code))
                Categories.Add(new CategoryTile(g.Id, g.Name));
        }
        catch { /* groups optional */ }

        ApplyCategory();
    }

    private void ApplyCategory()
    {
        MenuItems.Clear();
        IEnumerable<MenuTile> q = _allMenu;
        if (SelectedCategoryId != -1) q = q.Where(m => m.GroupId == SelectedCategoryId);
        if (!string.IsNullOrWhiteSpace(QuickSearch))
            q = q.Where(m => m.Name.Contains(QuickSearch) || m.Code.Contains(QuickSearch));
        foreach (var m in q.Take(60)) MenuItems.Add(m);
    }

    partial void OnQuickSearchChanged(string value) => ApplyCategory();

    [RelayCommand]
    private void SetOrderType(string? type) { if (!string.IsNullOrEmpty(type)) OrderType = type; }

    [RelayCommand]
    private void SelectCategory(CategoryTile? cat)
    {
        if (cat == null) return;
        SelectedCategoryId = cat.Id; SelectedCategoryName = cat.Name;
        ApplyCategory();
    }

    [RelayCommand]
    private void AddItem(MenuTile? tile)
    {
        if (tile == null) return;
        var existing = OrderLines.FirstOrDefault(l => l.ProductId == tile.Id);
        if (existing != null) { existing.Qty++; existing.Recalculate(); }
        else
        {
            var line = new OrderLine(OrderLines.Count + 1, tile.Id, tile.Name, 1, tile.Price, tile.TaxRate);
            line.PropertyChanged += (_, _) => Recalculate();
            OrderLines.Add(line);
        }
        Recalculate();
    }

    [RelayCommand] private void Inc(OrderLine? l) { if (l != null) { l.Qty++; l.Recalculate(); Recalculate(); } }
    [RelayCommand] private void Dec(OrderLine? l) { if (l != null) { if (l.Qty > 1) { l.Qty--; l.Recalculate(); } else OrderLines.Remove(l); Recalculate(); } }
    [RelayCommand] private void RemoveLine(OrderLine? l) { if (l != null) { OrderLines.Remove(l); Recalculate(); } }
    [RelayCommand] private void Clear() { OrderLines.Clear(); Recalculate(); }

    private void Recalculate()
    {
        SubTotal = OrderLines.Sum(l => l.Qty * l.UnitPrice);
        TaxAmount = TaxEnabled ? OrderLines.Sum(l => l.TaxAmount) : 0;
        ServiceAmount = ServiceEnabled ? Math.Round((SubTotal - Discount) * ServicePct / 100m) : 0;
        GrandTotal = SubTotal - Discount + TaxAmount + ServiceAmount + Tip;
        if (GrandTotal < 0) GrandTotal = 0;
        Remain = GrandTotal - CashPaid - PosPaid;
    }

    partial void OnDiscountChanged(decimal v) => Recalculate();
    partial void OnTipChanged(decimal v) => Recalculate();
    partial void OnServiceEnabledChanged(bool v) => Recalculate();
    partial void OnServicePctChanged(decimal v) => Recalculate();
    partial void OnTaxEnabledChanged(bool v) => Recalculate();
    partial void OnCashPaidChanged(decimal v) => Recalculate();
    partial void OnPosPaidChanged(decimal v) => Recalculate();

    private PrintDocumentData BuildBill(string title)
    {
        var lines = OrderLines.Select(l => new PrintLine(l.Row, "", l.Name, l.Qty, l.UnitPrice, 0, l.LineTotal)).ToList();
        var party = OrderType == "سالن" ? $"میز {TableNumber} — {People} نفر"
                  : OrderType == "پیک" ? $"پیک — {CustomerPhone}" : "بیرون‌بر";
        return new PrintDocumentData(title, OrderNumber, _calendar.GetCurrentPersianDate(),
            "سفارش", party, lines, SubTotal, Discount, TaxAmount + ServiceAmount, Tip,
            GrandTotal, CashPaid + PosPaid, Remain, $"{OrderType} | {CustomerName}");
    }

    [RelayCommand] private void PrintTicket() { if (OrderLines.Any()) try { _printService.PrintReceipt(BuildBill("تیکت آشپزخانه")); } catch { } }
    [RelayCommand] private void PrintBill() { if (OrderLines.Any()) try { _printService.PrintReceipt(BuildBill("صورتحساب")); } catch { } }

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        if (!OrderLines.Any()) { await _dialogService.ShowWarningAsync("سفارش خالی است."); return; }
        await ExecuteAsync(async () =>
        {
            var desc = $"رستوران {OrderType}" + (OrderType == "سالن" ? $" میز {TableNumber}/{People}نفر" : $" {CustomerName} {CustomerPhone}");
            var cmd = new CreateSalesInvoiceCommand(
                BranchId: _currentUser.BranchId ?? 1, FiscalYearId: 1,
                InvoiceDate: _calendar.GetCurrentPersianDate(), CustomerId: 1, WarehouseId: 1,
                InvoiceType: SamaHesab.Domain.Enums.InvoiceType.Sale, PriceLevel: "خرده",
                SalesRepId: null, DueDate: null, Description: desc,
                Shipping: 0, OtherCosts: ServiceAmount + Tip,
                Items: OrderLines.Select(l => new SalesInvoiceItemDto(
                    l.ProductId, l.Qty, l.UnitPrice, 0, TaxEnabled ? l.TaxRate : 0, null, null, null)).ToList(),
                InvoiceDiscount: Discount,
                PaidAmount: CashPaid + PosPaid,
                PaymentMethod: PosPaid > 0 && CashPaid == 0 ? "بانک" : "نقدی");
            var result = await _mediator.Send(cmd);
            if (!result.Succeeded) { await _dialogService.ShowErrorAsync(result.ErrorMessage); return; }
            try { _printService.PrintReceipt(BuildBill("صورتحساب")); } catch { }
            await _dialogService.ShowSuccessAsync($"سفارش ثبت شد (فاکتور #{result.Value}).");
            NewOrder();
        }, "در حال ثبت سفارش...");
    }

    [RelayCommand]
    private void NewOrder()
    {
        OrderLines.Clear(); Discount = 0; Tip = 0; CashPaid = 0; PosPaid = 0;
        TableNumber = 0; People = 1; CustomerName = null; CustomerPhone = null; CustomerAddress = null;
        OrderNumber = "R-" + DateTime.Now.ToString("yyMMddHHmm");
        Recalculate();
    }
}

public record CategoryTile(int Id, string Name);
public record MenuTile(int Id, string Code, string Name, decimal Price, int? GroupId, decimal TaxRate);

public partial class OrderLine : ObservableObject
{
    public int Row { get; set; }
    public int ProductId { get; }
    public string Name { get; }
    [ObservableProperty] private decimal _qty;
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _taxRate;
    [ObservableProperty] private decimal _taxAmount;
    [ObservableProperty] private decimal _lineTotal;

    public OrderLine(int row, int id, string name, decimal qty, decimal price, decimal taxRate)
    { Row = row; ProductId = id; Name = name; _qty = qty; _unitPrice = price; _taxRate = taxRate; Recalculate(); }

    public void Recalculate()
    { var sub = Qty * UnitPrice; TaxAmount = sub * TaxRate / 100m; LineTotal = sub + TaxAmount; }
}
