using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.Application.Sales.Commands;   // RecurringInvoice commands/DTOs
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Sales;

/// <summary>
/// F9-3 — فاکتورِ تکرارشونده: فهرستِ الگوها + تعریفِ الگوی جدید + تولیدِ فاکتورهای سررسیدشده.
/// روی بک‌اندِ آماده‌ی `SaveRecurringInvoiceCommand`/`GetRecurringInvoicesQuery`/`GenerateDueRecurringInvoicesCommand`.
/// </summary>
public partial class RecurringInvoiceListViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ApiClient _api;
    private readonly IPersianCalendarService _calendar;

    public ObservableCollection<RecurringRow> Items { get; } = new();
    public ObservableCollection<NewLineRow> NewLines { get; } = new();
    public List<CustomerItem> Customers { get; private set; } = new();
    public List<WarehouseItem> Warehouses { get; private set; } = new();
    public List<ProductSearchResult> Products { get; private set; } = new();
    public List<FreqOption> Frequencies { get; } = new() { new(0, "ماهانه"), new(1, "سالانه") };

    // فرمِ تعریفِ جدید
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private int _newCustomerId;
    [ObservableProperty] private int _newWarehouseId;
    [ObservableProperty] private int _newFrequency;
    [ObservableProperty] private string _newNextDate = string.Empty;
    [ObservableProperty] private ProductSearchResult? _lineProduct;
    [ObservableProperty] private decimal _lineQty = 1;
    [ObservableProperty] private decimal _linePrice;

    public RecurringInvoiceListViewModel(IMediator mediator, ApiClient api,
        IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _api = api; _calendar = calendar;
    }

    public override async Task LoadAsync()
    {
        NewNextDate = _calendar.GetCurrentPersianDate();
        var online = !string.IsNullOrWhiteSpace(_api.BaseUrl);

        // 🏛️ کلاینت→API، دسکتاپ→Application — دراپ‌داون‌ها از کوئری‌های مشترک.
        Customers = online
            ? (await _api.GetCustomersAsync()).Select(c => new CustomerItem(c.Id, c.Name, c.Mobile)).ToList()
            : (await _mediator.Send(new GetCustomersQuery())).Select(c => new CustomerItem(c.Id, c.Name, c.Mobile)).ToList();
        OnPropertyChanged(nameof(Customers));

        Warehouses = online
            ? (await _api.GetWarehousesAsync()).Select(w => new WarehouseItem(w.Id, w.Name)).ToList()
            : (await _mediator.Send(new GetWarehousesQuery())).Select(w => new WarehouseItem(w.Id, w.Name)).ToList();
        OnPropertyChanged(nameof(Warehouses));
        if (Warehouses.Any()) NewWarehouseId = Warehouses[0].Id;

        Products = online
            ? (await _api.GetProductListAsync()).Select(p => new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.SalePrice, p.TaxRate)).ToList()
            : (await _mediator.Send(new GetProductsQuery())).Select(p => new ProductSearchResult(p.Id, p.Code, p.Name, p.Barcode, p.SalePrice, p.TaxRate)).ToList();
        OnPropertyChanged(nameof(Products));

        await ReloadListAsync();
    }

    private async Task ReloadListAsync()
    {
        var names = Customers.ToDictionary(c => c.Id, c => c.Name);
        Items.Clear();
        foreach (var d in await _mediator.Send(new GetRecurringInvoicesQuery()))
            Items.Add(new RecurringRow(d.Id, d.Name, names.TryGetValue(d.CustomerId, out var n) ? n : $"#{d.CustomerId}",
                d.Frequency == 1 ? "سالانه" : "ماهانه", d.NextDate, d.LastGeneratedDate ?? "—", d.IsActive));
    }

    partial void OnLineProductChanged(ProductSearchResult? value)
    { if (value != null) LinePrice = value.Price; }

    [RelayCommand]
    private void AddLine()
    {
        if (LineProduct == null || LineQty <= 0) return;
        NewLines.Add(new NewLineRow(LineProduct.Id, LineProduct.Name, LineQty, LinePrice, LineProduct.TaxRate));
        LineProduct = null; LineQty = 1; LinePrice = 0;
    }

    [RelayCommand] private void RemoveLine(NewLineRow? row) { if (row != null) NewLines.Remove(row); }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName)) { await _dialogService.ShowWarningAsync("نامِ الگو الزامی است."); return; }
        if (NewCustomerId <= 0) { await _dialogService.ShowWarningAsync("مشتری را انتخاب کنید."); return; }
        if (!NewLines.Any()) { await _dialogService.ShowWarningAsync("حداقل یک ردیفِ کالا اضافه کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var cmd = new SaveRecurringInvoiceCommand(NewName, NewCustomerId, NewWarehouseId, NewFrequency, NewNextDate,
                NewLines.Select(l => new RecurringInvoiceLineDto(l.ProductId, l.Quantity, l.UnitPrice, l.TaxPct)).ToList());
            var r = await _mediator.Send(cmd);
            if (!r.Succeeded) { await _dialogService.ShowErrorAsync(r.ErrorMessage ?? "خطا در ذخیرهٔ الگو."); return; }
            await _dialogService.ShowSuccessAsync($"الگوی تکرارشونده «{NewName}» ذخیره شد.");
            NewName = string.Empty; NewCustomerId = 0; NewLines.Clear();
            await ReloadListAsync();
        }, "در حال ذخیرهٔ الگو...");
    }

    [RelayCommand]
    private async Task GenerateDueAsync()
    {
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new GenerateDueRecurringInvoicesCommand(_calendar.GetCurrentPersianDate()));
            if (!r.Succeeded) { await _dialogService.ShowErrorAsync(r.ErrorMessage ?? "خطا در تولید."); return; }
            await _dialogService.ShowSuccessAsync($"{r.Value!.Generated} فاکتورِ سررسیدشده تولید شد.");
            await ReloadListAsync();
        }, "در حال تولیدِ فاکتورهای سررسیدشده...");
    }

    [RelayCommand] private async Task RefreshAsync() => await ReloadListAsync();
}

public record RecurringRow(int Id, string Name, string CustomerName, string Frequency,
    string NextDate, string LastGenerated, bool IsActive);
public record NewLineRow(int ProductId, string Name, decimal Quantity, decimal UnitPrice, decimal TaxPct);
public record FreqOption(int Id, string Name);
