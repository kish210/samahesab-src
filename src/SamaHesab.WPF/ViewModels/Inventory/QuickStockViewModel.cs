using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>
/// L3 — انتقال/تعدیلِ سریعِ انبار: یک فرمِ تک‌ضربه روی Commandهای موجود
/// (<see cref="TransferStockCommand"/> / <see cref="AdjustStockCommand"/>). برای ثبتِ سریع بدونِ سندِ چندردیفی.
/// حالت: انتقال (مبدأ→مقصد) یا تعدیل (موجودیِ صحیحِ جدید). موجودیِ جاریِ کالا در انبار نمایش داده می‌شود.
/// </summary>
public partial class QuickStockViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ApiClient _api;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private bool _isAdjust;               // false=انتقال، true=تعدیل
    [ObservableProperty] private int _fromWarehouseId;         // در حالتِ تعدیل = انبارِ هدف
    [ObservableProperty] private int _toWarehouseId;
    [ObservableProperty] private ProductPick? _selectedProduct;
    [ObservableProperty] private decimal _quantity = 1;
    [ObservableProperty] private string _date = string.Empty;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private decimal _currentStock;
    [ObservableProperty] private string _resultMessage = string.Empty;
    [ObservableProperty] private bool _resultOk;
    public bool HasResult => !string.IsNullOrWhiteSpace(ResultMessage);
    partial void OnResultMessageChanged(string value) => OnPropertyChanged(nameof(HasResult));

    public List<WarehousePick> Warehouses { get; private set; } = new();
    public List<ProductPick> Products { get; private set; } = new();

    public string ModeTitle => IsAdjust ? "تعدیلِ سریعِ موجودی" : "انتقالِ سریعِ انبار";
    public string QtyLabel => IsAdjust ? "موجودیِ صحیحِ جدید" : "مقدارِ انتقال";
    public string FromLabel => IsAdjust ? "انبار" : "از انبارِ (مبدأ)";
    public bool IsTransfer => !IsAdjust;

    public QuickStockViewModel(IMediator mediator, ApiClient api, IPersianCalendarService calendar,
        IDialogService d, INavigationService n) : base(d, n)
    { _mediator = mediator; _api = api; _calendar = calendar; }

    partial void OnIsAdjustChanged(bool value)
    {
        OnPropertyChanged(nameof(ModeTitle)); OnPropertyChanged(nameof(QtyLabel));
        OnPropertyChanged(nameof(FromLabel)); OnPropertyChanged(nameof(IsTransfer));
    }
    partial void OnFromWarehouseIdChanged(int value) => _ = RefreshStockAsync();
    partial void OnSelectedProductChanged(ProductPick? value) => _ = RefreshStockAsync();

    public override async Task LoadAsync()
    {
        Date = _calendar.GetCurrentPersianDate();
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

    private async Task RefreshStockAsync()
    {
        if (SelectedProduct is null || FromWarehouseId <= 0) { CurrentStock = 0; return; }
        var rows = !string.IsNullOrWhiteSpace(_api.BaseUrl)
            ? (await _api.GetWarehouseStockAsync(FromWarehouseId)).Select(s => (s.ProductId, s.Quantity))
            : (await _mediator.Send(new GetWarehouseStockQuery(FromWarehouseId))).Select(s => (s.ProductId, s.Quantity));
        CurrentStock = rows.Where(s => s.ProductId == SelectedProduct.Id).Sum(s => s.Quantity);
    }

    [RelayCommand] private void SetMode(string? m) => IsAdjust = m == "adjust";

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (SelectedProduct is null) { await _dialogService.ShowErrorAsync("کالا را انتخاب کنید."); return; }
        if (FromWarehouseId <= 0) { await _dialogService.ShowErrorAsync("انبار را انتخاب کنید."); return; }
        if (!IsAdjust && Quantity <= 0) { await _dialogService.ShowErrorAsync("مقدار باید بزرگتر از صفر باشد."); return; }
        // UX-CORE-AUDIT — اگر فقط یک انبار وجود دارد، LoadAsync هرگز ToWarehouseId را ست نمی‌کند (می‌ماند ۰) و
        // چون FromWarehouseId (مثلاً ۱) با ۰ برابر نیست، اعتبارسنجیِ قبلی رد می‌شد و انتقال به یک StockItemِ
        // جدید با WarehouseId=۰ (انبارِ ناموجود) ثبت می‌شد — موجودی از انبارِ واقعی کم می‌شد ولی به‌جایِ
        // درستی اضافه نمی‌شد. حالا صریحاً چک می‌شود.
        if (!IsAdjust && ToWarehouseId <= 0) { await _dialogService.ShowErrorAsync("انبارِ مقصد را انتخاب کنید."); return; }
        if (!IsAdjust && FromWarehouseId == ToWarehouseId) { await _dialogService.ShowErrorAsync("انبارِ مبدأ و مقصد یکسان است."); return; }

        await ExecuteAsync(async () =>
        {
            Result res = IsAdjust
                ? await _mediator.Send(new AdjustStockCommand(FromWarehouseId, SelectedProduct.Id, Quantity, Date, Description))
                : await _mediator.Send(new TransferStockCommand(FromWarehouseId, ToWarehouseId, SelectedProduct.Id, Quantity, Date, Description));
            ResultOk = res.Succeeded;
            if (res.Succeeded)
            {
                ResultMessage = IsAdjust
                    ? $"موجودیِ «{SelectedProduct.Display}» به {Quantity:#,##0} اصلاح شد."
                    : $"{Quantity:#,##0} واحد «{SelectedProduct.Display}» منتقل شد.";
                var keepFrom = FromWarehouseId;
                SelectedProduct = null; Quantity = 1; Description = null;
                FromWarehouseId = keepFrom;   // انبار برای ورودِ بعدی حفظ می‌شود (سرعت)
                await RefreshStockAsync();
            }
            else { ResultMessage = res.ErrorMessage; }
        }, "در حال ثبت...");
    }
}
