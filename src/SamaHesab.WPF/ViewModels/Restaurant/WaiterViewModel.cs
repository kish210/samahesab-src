using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.POS;     // reuse CategoryTile / MenuTile
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Restaurant;

/// <summary>
/// صفحه‌ی لمسی گارسون: انتخاب سالن/میز، باز کردن سفارش روی میز، افزودن آیتم،
/// ارسال به آشپزخانه و تسویه. کل ارتباط از طریق Web API است (نه دیتابیس).
/// </summary>
public partial class WaiterViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly IPrintService _print;
    private readonly IPersianCalendarService _calendar;

    public ObservableCollection<WaiterHall> Halls { get; } = new();
    public ObservableCollection<WaiterTable> Tables { get; } = new();
    public ObservableCollection<CategoryTile> Categories { get; } = new();
    public ObservableCollection<MenuTile> MenuItems { get; } = new();
    public ObservableCollection<WaiterOrderLine> OrderLines { get; } = new();

    [ObservableProperty] private WaiterHall? _selectedHall;
    [ObservableProperty] private bool _showOrder;          // false=صفحه میزها, true=صفحه سفارش
    [ObservableProperty] private int _currentOrderId;
    [ObservableProperty] private string _currentTableName = string.Empty;
    [ObservableProperty] private string _orderNumber = string.Empty;
    [ObservableProperty] private int _selectedCategoryId = -1;
    [ObservableProperty] private string _quickSearch = string.Empty;
    [ObservableProperty] private decimal _subTotal;
    [ObservableProperty] private decimal _serviceTax;      // مالیات و خدمات (جمع کل − جمع اقلام)
    [ObservableProperty] private decimal _grandTotal;
    [ObservableProperty] private int _currentSeats;
    [ObservableProperty] private string _statusText = string.Empty;

    private List<MenuTile> _allMenu = new();
    private readonly System.Windows.Threading.DispatcherTimer _elapsedTimer;

    public WaiterViewModel(ApiClient api, IPrintService print, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _api = api; _print = print; _calendar = calendar;
        // U8 — تازه‌سازیِ زمانِ سپری‌شدهٔ میزها هر ۳۰ ثانیه (بدون فراخوانِ سرور).
        _elapsedTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _elapsedTimer.Tick += (_, _) => { foreach (var t in Tables) t.RefreshElapsed(); };
        _elapsedTimer.Start();
    }

    public override async Task LoadAsync()
    {
        await LoadHallsAsync();
        if (_allMenu.Count == 0) await LoadMenuAsync();
    }

    private async Task LoadHallsAsync()
    {
        var halls = await _api.GetHallsAsync();
        var previouslySelected = SelectedHall?.Id;
        Halls.Clear();
        foreach (var h in halls) Halls.Add(new WaiterHall(h.Id, h.Name, h.Tables));
        SelectedHall = Halls.FirstOrDefault(h => h.Id == previouslySelected) ?? Halls.FirstOrDefault();
        RefreshTables();
        StatusText = $"{Halls.Count} سالن — {Halls.Sum(h => h.Tables.Count)} میز";
    }

    private async Task LoadMenuAsync()
    {
        var prods = await _api.SearchProductsAsync("");
        _allMenu = prods.Select(p => new MenuTile(p.Id, p.Code, p.Name, p.SalePrice, p.GroupId, p.TaxRate)).ToList();
        Categories.Clear();
        Categories.Add(new CategoryTile(-1, "همه"));
        foreach (var g in await _api.GetGroupsAsync()) Categories.Add(new CategoryTile(g.Id, g.Name));
        ApplyCategory();
    }

    partial void OnSelectedHallChanged(WaiterHall? value) => RefreshTables();

    private void RefreshTables()
    {
        Tables.Clear();
        if (SelectedHall == null) return;
        foreach (var t in SelectedHall.Tables.OrderBy(t => t.Name))
            Tables.Add(new WaiterTable(t.Id, t.Name, t.Capacity, t.Status, t.StatusCode, t.CurrentOrderId, 0, t.OccupiedSince));
    }

    [RelayCommand]
    private void SelectHall(WaiterHall? hall) { if (hall != null) SelectedHall = hall; }

    /// <summary>کلیک روی میز: اگر آزاد است سفارش باز می‌کند، در غیر این صورت سفارش جاری را باز می‌کند.</summary>
    [RelayCommand]
    private async Task OpenTableAsync(WaiterTable? table)
    {
        if (table == null) return;
        await ExecuteAsync(async () =>
        {
            int orderId;
            if (table.CurrentOrderId is int existing && existing > 0)
            {
                orderId = existing;
            }
            else
            {
                var (ok, id, error) = await _api.OpenOrderAsync(0 /*DineIn*/, table.Id, 1);
                if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در باز کردن میز."); return; }
                orderId = id;
            }
            CurrentOrderId = orderId;
            CurrentTableName = table.Name;
            CurrentSeats = table.Capacity;
            if (_allMenu.Count == 0) await LoadMenuAsync();
            await ReloadOrderAsync();
            ShowOrder = true;
        }, "در حال باز کردن میز...");
    }

    /// <summary>U7 — رزرو یا لغوِ رزروِ میز (تاگل). میزِ آزاد رزرو می‌شود؛ میزِ رزرو آزاد می‌شود.</summary>
    [RelayCommand]
    private async Task ReserveTableAsync(WaiterTable? table)
    {
        if (table == null) return;
        var reserve = table.StatusCode != 2;   // 2=Reserved → لغو؛ غیرِ آن → رزرو
        await ExecuteAsync(async () =>
        {
            var (ok, error) = await _api.ReserveTableAsync(table.Id, reserve);
            if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در رزرو میز."); return; }
            await LoadHallsAsync();
        }, reserve ? "در حال رزرو میز..." : "در حال لغو رزرو...");
    }

    private async Task ReloadOrderAsync()
    {
        var o = await _api.GetOrderAsync(CurrentOrderId);
        OrderLines.Clear();
        if (o == null) return;
        OrderNumber = o.OrderNumber;
        foreach (var i in o.Items)
            OrderLines.Add(new WaiterOrderLine(i.Id, i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.LineTotal, i.Status, i.Notes, i.StatusCode));
        SubTotal = o.SubTotal;
        GrandTotal = o.GrandTotal;
        ServiceTax = o.GrandTotal - o.SubTotal;
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
    private void SelectCategory(CategoryTile? cat)
    {
        if (cat == null) return;
        SelectedCategoryId = cat.Id;
        ApplyCategory();
    }

    [RelayCommand]
    private async Task AddMenuItemAsync(MenuTile? tile)
    {
        if (tile == null || CurrentOrderId == 0) return;
        var (ok, error) = await _api.AddOrderItemAsync(CurrentOrderId, tile.Id, tile.Name, 1, tile.Price);
        if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در افزودن آیتم."); return; }
        await ReloadOrderAsync();
    }

    /// <summary>افزایش تعداد یک ردیف سفارش (همان کالا را دوباره به سفارش می‌افزاید).</summary>
    [RelayCommand]
    private async Task IncLineAsync(WaiterOrderLine? line)
    {
        if (line == null || CurrentOrderId == 0) return;
        var (ok, error) = await _api.AddOrderItemAsync(CurrentOrderId, line.ProductId, line.Name, 1, line.UnitPrice);
        if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در افزودن."); return; }
        await ReloadOrderAsync();
    }

    /// <summary>کاهش تعداد ردیف (به صفر برسد، حذف می‌شود). فقط ردیف ارسال‌نشده به آشپزخانه.</summary>
    [RelayCommand]
    private async Task DecLineAsync(WaiterOrderLine? line)
    {
        if (line == null || CurrentOrderId == 0) return;
        var newQty = line.Qty - 1;
        var (ok, error) = await _api.ChangeOrderItemQtyAsync(CurrentOrderId, line.Id, newQty);
        if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در تغییر تعداد."); return; }
        await ReloadOrderAsync();
    }

    /// <summary>حذف کامل یک ردیف از سفارش.</summary>
    [RelayCommand]
    private async Task RemoveLineAsync(WaiterOrderLine? line)
    {
        if (line == null || CurrentOrderId == 0) return;
        var (ok, error) = await _api.RemoveOrderItemAsync(CurrentOrderId, line.Id);
        if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در حذف ردیف."); return; }
        await ReloadOrderAsync();
    }

    /// <summary>ثبت یادداشت آشپزخانه برای یک ردیف (مثل «بدون پیاز»).</summary>
    [RelayCommand]
    private async Task EditNoteAsync(WaiterOrderLine? line)
    {
        if (line == null || CurrentOrderId == 0) return;
        var note = await _dialogService.PromptAsync("یادداشت آشپزخانه برای این ردیف:", "یادداشت", line.Notes ?? "");
        if (note == null) return;
        var (ok, error) = await _api.SetOrderItemNotesAsync(CurrentOrderId, line.Id, string.IsNullOrWhiteSpace(note) ? null : note);
        if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در ثبت یادداشت."); return; }
        await ReloadOrderAsync();
    }

    /// <summary>انتقال سفارش جاری به میز دیگر (شماره‌ی میز مقصد را می‌پرسد).</summary>
    [RelayCommand]
    private async Task TransferTableAsync()
    {
        if (CurrentOrderId == 0) { await _dialogService.ShowWarningAsync("ابتدا یک میز را باز کنید."); return; }
        var name = await _dialogService.PromptAsync("شماره/نام میز مقصد را وارد کنید:", "انتقال میز");
        if (string.IsNullOrWhiteSpace(name)) return;
        var dest = AllTables().FirstOrDefault(t => t.Name == name!.Trim());
        if (dest == null) { await _dialogService.ShowErrorAsync($"میز «{name}» یافت نشد."); return; }
        if (dest.Id == 0) { await _dialogService.ShowErrorAsync("میز مقصد نامعتبر است."); return; }
        await ExecuteAsync(async () =>
        {
            var (ok, error) = await _api.MoveOrderTableAsync(CurrentOrderId, dest.Id);
            if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در انتقال میز."); return; }
            CurrentTableName = dest.Name;
            await LoadHallsAsync();
            await _dialogService.ShowSuccessAsync($"سفارش به میز {dest.Name} منتقل شد.");
        }, "در حال انتقال میز...");
    }

    private IEnumerable<WaiterTable> AllTables()
        => Halls.SelectMany(h => h.Tables.Select(t =>
            new WaiterTable(t.Id, t.Name, t.Capacity, t.Status, t.StatusCode, t.CurrentOrderId, 0, t.OccupiedSince)));

    /// <summary>چاپ صورتحساب میز روی پرینتر حرارتی (۸۰م م).</summary>
    [RelayCommand]
    private async Task BillAsync()
    {
        if (CurrentOrderId == 0 || !OrderLines.Any()) { await _dialogService.ShowWarningAsync("سفارشی برای صورتحساب نیست."); return; }
        try
        {
            int row = 1;
            var lines = OrderLines.Select(l => new PrintLine(
                row++, "", l.Name + (string.IsNullOrWhiteSpace(l.Notes) ? "" : $" ({l.Notes})"),
                l.Qty, l.UnitPrice, 0, l.LineTotal)).ToList();
            var data = new PrintDocumentData(
                DocTitle: "صورتحساب رستوران",
                Number: OrderNumber,
                Date: _calendar.GetCurrentPersianDate(),
                PartyLabel: "میز", PartyName: CurrentTableName,
                Lines: lines,
                SubTotal: SubTotal, Discount: 0, Tax: ServiceTax, Shipping: 0,
                GrandTotal: GrandTotal, Paid: 0, Remain: GrandTotal,
                Description: $"تعداد نفرات: {CurrentSeats}");
            _print.PrintReceipt(data);
        }
        catch (Exception ex) { await _dialogService.ShowErrorAsync("خطا در چاپ صورتحساب: " + ex.Message); }
    }

    [RelayCommand]
    private async Task SendToKitchenAsync()
    {
        if (CurrentOrderId == 0) return;
        // عکس‌برداری از ردیف‌های فعلی برای چاپ تیکت آشپزخانه پیش از تغییر وضعیت
        var snapshot = OrderLines.ToList();
        await ExecuteAsync(async () =>
        {
            var (ok, error) = await _api.SendToKitchenAsync(CurrentOrderId);
            if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در ارسال به آشپزخانه."); return; }
            PrintKitchenTicket(snapshot);   // best-effort (بدون قیمت)
            await ReloadOrderAsync();
            await _dialogService.ShowSuccessAsync("سفارش به آشپزخانه ارسال شد.");
        }, "در حال ارسال به آشپزخانه...");
    }

    /// <summary>چاپ تیکت آشپزخانه (KOT) — فقط نام و تعداد، بدون قیمت. خطا مانع ارسال نمی‌شود.</summary>
    private void PrintKitchenTicket(IReadOnlyList<WaiterOrderLine> items)
    {
        try
        {
            if (items.Count == 0) return;
            int row = 1;
            var lines = items.Select(l => new PrintLine(
                row++, "", l.Name + (string.IsNullOrWhiteSpace(l.Notes) ? "" : $" — {l.Notes}"),
                l.Qty, 0, 0, 0)).ToList();
            var data = new PrintDocumentData(
                DocTitle: "تیکت آشپزخانه (KOT)",
                Number: OrderNumber, Date: _calendar.GetCurrentPersianDate(),
                PartyLabel: "میز", PartyName: CurrentTableName, Lines: lines,
                SubTotal: 0, Discount: 0, Tax: 0, Shipping: 0,
                GrandTotal: 0, Paid: 0, Remain: 0,
                Description: $"تعداد نفرات: {CurrentSeats}");
            _print.PrintReceipt(data);
        }
        catch { /* عدم وجود پرینتر نباید جلوی ارسال به آشپزخانه را بگیرد */ }
    }

    [RelayCommand]
    private async Task SettleAsync()
    {
        if (CurrentOrderId == 0) return;
        if (!OrderLines.Any()) { await _dialogService.ShowWarningAsync("سفارش خالی است."); return; }
        await ExecuteAsync(async () =>
        {
            var (ok, error) = await _api.SettleOrderAsync(CurrentOrderId, GrandTotal);
            if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در تسویه."); return; }
            await _dialogService.ShowSuccessAsync($"میز {CurrentTableName} تسویه شد.");
            await BackToTablesAsync();
        }, "در حال تسویه...");
    }

    [RelayCommand]
    private async Task BackToTablesAsync()
    {
        ShowOrder = false;
        CurrentOrderId = 0;
        OrderLines.Clear();
        await LoadHallsAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadHallsAsync();
}

public record WaiterHall(int Id, string Name, List<ApiTable> Tables);
public record WaiterOrderLine(int Id, int ProductId, string Name, decimal Qty, decimal UnitPrice, decimal LineTotal, string Status, string? Notes, int StatusCode = 0);

public partial class WaiterTable : ObservableObject
{
    public int Id { get; }
    public string Name { get; }
    public int Capacity { get; }
    public string Status { get; }
    public int StatusCode { get; }   // 0=Free 1=Occupied 2=Reserved 3=Billing
    public int? CurrentOrderId { get; }
    public decimal OpenAmount { get; }            // R17: مبلغ سفارش بازِ میز (۰ = نمایش متن وضعیت)
    public bool ShowAmount => OpenAmount > 0;     // برای کارت میز
    // U7 — تاگلِ رزرو فقط روی میزِ آزاد (0) یا رزرو (2) معنا دارد (نه مشغول/تسویه).
    public bool CanToggleReserve => StatusCode == 0 || StatusCode == 2;
    public string ReserveLabel => StatusCode == 2 ? "لغو رزرو" : "رزرو";

    // U8 — مدتِ سپری‌شده از بازشدنِ میز (برای میزِ مشغول/در حال تسویه).
    public DateTime? OccupiedSince { get; }
    public bool ShowElapsed => OccupiedSince is not null && (StatusCode == 1 || StatusCode == 3);
    public string ElapsedText
    {
        get
        {
            if (OccupiedSince is not DateTime since) return string.Empty;
            var mins = (int)Math.Max(0, (DateTime.Now - since).TotalMinutes);
            return mins < 60 ? $"⏱ {mins} دقیقه" : $"⏱ {mins / 60}:{mins % 60:D2} ساعت";
        }
    }
    /// <summary>U8 — تازه‌سازیِ نمایشِ زمانِ سپری‌شده (توسطِ تایمرِ VM فراخوانی می‌شود).</summary>
    public void RefreshElapsed() { OnPropertyChanged(nameof(ElapsedText)); }

    public WaiterTable(int id, string name, int capacity, string status, int statusCode, int? currentOrderId,
        decimal openAmount = 0, DateTime? occupiedSince = null)
    { Id = id; Name = name; Capacity = capacity; Status = status; StatusCode = statusCode; CurrentOrderId = currentOrderId; OpenAmount = openAmount; OccupiedSince = occupiedSince; }
}
