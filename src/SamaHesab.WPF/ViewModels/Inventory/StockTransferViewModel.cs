using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>انتقال بین انبار — 🏛️ الگوی API-only: کلاینت→API، دسکتاپ→Application. بدونِ ریپازیتوریِ مستقیم.</summary>
public partial class StockTransferViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ApiClient _api;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private int _fromWarehouseId;
    [ObservableProperty] private int _toWarehouseId;
    [ObservableProperty] private ProductPick? _selectedProduct;
    [ObservableProperty] private decimal _quantity = 1;
    [ObservableProperty] private string _transferDate = string.Empty;
    [ObservableProperty] private string? _description;

    [ObservableProperty] private int _lineCount;
    [ObservableProperty] private decimal _totalQty;

    public List<WarehousePick> Warehouses { get; private set; } = new();
    public List<ProductPick> Products { get; private set; } = new();
    public ObservableCollection<TransferLineRow> Lines { get; } = new();

    public string FromWarehouseName => Warehouses.FirstOrDefault(w => w.Id == FromWarehouseId)?.Name ?? "—";
    public string ToWarehouseName => Warehouses.FirstOrDefault(w => w.Id == ToWarehouseId)?.Name ?? "—";

    partial void OnFromWarehouseIdChanged(int value)
    {
        OnPropertyChanged(nameof(FromWarehouseName));
        _ = RefreshSourceStockAsync();   // موجودی مبدأ ردیف‌ها با تغییر انبار به‌روز شود
    }
    partial void OnToWarehouseIdChanged(int value) => OnPropertyChanged(nameof(ToWarehouseName));

    private async Task RefreshSourceStockAsync()
    {
        foreach (var l in Lines.ToList())
            l.SourceStock = await SourceStockAsync(l.ProductId);
    }

    public StockTransferViewModel(IMediator mediator, ApiClient api, ICurrentUserService currentUser,
        IPersianCalendarService calendar, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _api = api; _currentUser = currentUser; _calendar = calendar;
    }

    /// <summary>موجودی کالا در انبار مبدأ (🏛️ کلاینت→API، دسکتاپ→Application).</summary>
    private async Task<decimal> SourceStockAsync(int productId)
    {
        if (FromWarehouseId <= 0) return 0;
        var rows = !string.IsNullOrWhiteSpace(_api.BaseUrl)
            ? (await _api.GetWarehouseStockAsync(FromWarehouseId)).Select(s => (s.ProductId, s.Quantity))
            : (await _mediator.Send(new GetWarehouseStockQuery(FromWarehouseId))).Select(s => (s.ProductId, s.Quantity));
        return rows.Where(s => s.ProductId == productId).Sum(s => s.Quantity);
    }

    public override async Task LoadAsync()
    {
        TransferDate = _calendar.GetCurrentPersianDate();
        var online = !string.IsNullOrWhiteSpace(_api.BaseUrl);
        Warehouses = online
            ? (await _api.GetWarehousesAsync()).Select(w => new WarehousePick(w.Id, w.Name)).ToList()
            : (await _mediator.Send(new GetWarehousesQuery())).Select(w => new WarehousePick(w.Id, w.Name)).ToList();
        OnPropertyChanged(nameof(Warehouses));
        if (Warehouses.Count > 0) FromWarehouseId = Warehouses[0].Id;
        if (Warehouses.Count > 1) ToWarehouseId = Warehouses[1].Id;

        Products = online
            ? (await _api.GetProductListAsync()).Select(p => new ProductPick(p.Id, $"{p.Code} - {p.Name}")).ToList()
            : (await _mediator.Send(new GetProductsQuery())).Select(p => new ProductPick(p.Id, $"{p.Code} - {p.Name}")).ToList();
        OnPropertyChanged(nameof(Products));
    }

    /// <summary>افزودن کالای انتخاب‌شده به فهرست ردیف‌های انتقال (با واکشی موجودی مبدأ).</summary>
    [RelayCommand]
    private async Task AddLineAsync()
    {
        if (SelectedProduct == null) { await _dialogService.ShowErrorAsync("کالا را انتخاب کنید."); return; }
        if (Quantity <= 0) { await _dialogService.ShowErrorAsync("مقدار باید بزرگتر از صفر باشد."); return; }
        var existing = Lines.FirstOrDefault(l => l.ProductId == SelectedProduct.Id);
        if (existing != null) existing.Qty += Quantity;
        else
        {
            var src = await SourceStockAsync(SelectedProduct.Id);
            Lines.Add(new TransferLineRow(SelectedProduct.Id, Lines.Count + 1, SelectedProduct.Display, Quantity, src));
        }
        SelectedProduct = null; Quantity = 1;
        Recalc();
    }

    [RelayCommand]
    private void RemoveLine(TransferLineRow? row)
    {
        if (row == null) return;
        Lines.Remove(row);
        var n = 1; foreach (var l in Lines) l.RowNumber = n++;
        Recalc();
    }

    private void Recalc() { LineCount = Lines.Count; TotalQty = Lines.Sum(l => l.Qty); }

    /// <summary>ثبت همه‌ی ردیف‌ها: برای هر قلم یک انتقال انبار.</summary>
    [RelayCommand]
    private async Task TransferAsync()
    {
        if (Lines.Count == 0) { await _dialogService.ShowErrorAsync("حداقل یک ردیف اضافه کنید."); return; }
        if (FromWarehouseId == ToWarehouseId) { await _dialogService.ShowErrorAsync("انبار مبدأ و مقصد یکسان است."); return; }
        var over = Lines.FirstOrDefault(l => l.IsShortage);
        if (over != null) { await _dialogService.ShowErrorAsync($"موجودی مبدأ برای «{over.Display}» کافی نیست (موجودی {over.SourceStock:#,##0}، درخواست {over.Qty:#,##0})."); return; }
        await ExecuteAsync(async () =>
        {
            int ok = 0; string? lastErr = null;
            foreach (var l in Lines.ToList())
            {
                var result = await _mediator.Send(new TransferStockCommand(
                    FromWarehouseId, ToWarehouseId, l.ProductId, l.Qty, TransferDate, Description));
                if (result.Succeeded) ok++; else lastErr = result.ErrorMessage;
            }
            if (ok == Lines.Count)
            {
                await _dialogService.ShowSuccessAsync($"انتقال {ok} قلم با موفقیت ثبت شد.");
                Lines.Clear(); Description = null; Recalc();
            }
            else await _dialogService.ShowErrorAsync($"{ok} قلم ثبت شد؛ خطا: {lastErr}");
        }, "در حال ثبت انتقال...");
    }
}

public record WarehousePick(int Id, string Name);
public record ProductPick(int Id, string Display);

public partial class TransferLineRow : ObservableObject
{
    public int ProductId { get; }
    public string Display { get; }
    [ObservableProperty] private int _rowNumber;
    [ObservableProperty] private decimal _qty;
    [ObservableProperty] private decimal _sourceStock;

    /// <summary>مانده‌ی انبار مبدأ پس از انتقال.</summary>
    public decimal RemainingAfter => SourceStock - Qty;
    /// <summary>درخواست بیش از موجودی مبدأ؟</summary>
    public bool IsShortage => Qty > SourceStock;

    partial void OnQtyChanged(decimal value) { OnPropertyChanged(nameof(RemainingAfter)); OnPropertyChanged(nameof(IsShortage)); }
    partial void OnSourceStockChanged(decimal value) { OnPropertyChanged(nameof(RemainingAfter)); OnPropertyChanged(nameof(IsShortage)); }

    public TransferLineRow(int productId, int rowNumber, string display, decimal qty, decimal sourceStock)
    { ProductId = productId; _rowNumber = rowNumber; Display = display; _qty = qty; _sourceStock = sourceStock; }
}
