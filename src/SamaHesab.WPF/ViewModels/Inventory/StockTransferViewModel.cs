using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Inventory;

public partial class StockTransferViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IProductRepository _productRepo;
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

    partial void OnFromWarehouseIdChanged(int value) => OnPropertyChanged(nameof(FromWarehouseName));
    partial void OnToWarehouseIdChanged(int value) => OnPropertyChanged(nameof(ToWarehouseName));

    public StockTransferViewModel(IMediator mediator, IWarehouseRepository warehouseRepo,
        IProductRepository productRepo, ICurrentUserService currentUser,
        IPersianCalendarService calendar, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _warehouseRepo = warehouseRepo; _productRepo = productRepo;
        _currentUser = currentUser; _calendar = calendar;
    }

    public override async Task LoadAsync()
    {
        var companyId = _currentUser.CompanyId ?? 1;
        TransferDate = _calendar.GetCurrentPersianDate();
        var whs = await _warehouseRepo.GetByCompanyAsync(companyId);
        Warehouses = whs.Select(w => new WarehousePick(w.Id, w.Name)).ToList();
        OnPropertyChanged(nameof(Warehouses));
        if (Warehouses.Count > 0) FromWarehouseId = Warehouses[0].Id;
        if (Warehouses.Count > 1) ToWarehouseId = Warehouses[1].Id;

        var prods = await _productRepo.SearchAsync(companyId, "");
        Products = prods.Select(p => new ProductPick(p.Id, $"{p.Code} - {p.Name}")).ToList();
        OnPropertyChanged(nameof(Products));
    }

    /// <summary>افزودن کالای انتخاب‌شده به فهرست ردیف‌های انتقال.</summary>
    [RelayCommand]
    private void AddLine()
    {
        if (SelectedProduct == null) { _ = _dialogService.ShowErrorAsync("کالا را انتخاب کنید."); return; }
        if (Quantity <= 0) { _ = _dialogService.ShowErrorAsync("مقدار باید بزرگتر از صفر باشد."); return; }
        var existing = Lines.FirstOrDefault(l => l.ProductId == SelectedProduct.Id);
        if (existing != null) existing.Qty += Quantity;
        else Lines.Add(new TransferLineRow(SelectedProduct.Id, Lines.Count + 1, SelectedProduct.Display, Quantity));
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

    public TransferLineRow(int productId, int rowNumber, string display, decimal qty)
    { ProductId = productId; _rowNumber = rowNumber; Display = display; _qty = qty; }
}
