using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>
/// عمق انبار (INV-1): مدیریت بچ و سریالِ کالاها + گزارش کالاهای رو به انقضا.
/// </summary>
public partial class BatchSerialViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IProductRepository _products;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;

    public ObservableCollection<ProductPick> Products { get; } = new();
    public ObservableCollection<BatchDto> Batches { get; } = new();
    public ObservableCollection<SerialDto> Serials { get; } = new();
    public ObservableCollection<ExpiringBatchDto> Expiring { get; } = new();

    [ObservableProperty] private int _selectedProductId;
    [ObservableProperty] private int _expiryHorizon = 60;

    // فرم بچ جدید
    [ObservableProperty] private string _batchNumber = string.Empty;
    [ObservableProperty] private string _productionDate = string.Empty;
    [ObservableProperty] private string _expiryDate = string.Empty;
    [ObservableProperty] private decimal _batchQuantity;
    [ObservableProperty] private decimal _batchPurchasePrice;

    // فرم سریال جدید
    [ObservableProperty] private string _serialNumber = string.Empty;
    [ObservableProperty] private decimal _serialPurchasePrice;

    public BatchSerialViewModel(IMediator mediator, IProductRepository products,
        ICurrentUserService currentUser, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _products = products; _currentUser = currentUser; _calendar = calendar;
    }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var prods = await _products.SearchAsync(_currentUser.CompanyId ?? 1, "");
            Products.Clear();
            foreach (var p in prods.OrderBy(p => p.Code))
                Products.Add(new ProductPick(p.Id, $"{p.Code} - {p.Name}"));
            SelectedProductId = Products.FirstOrDefault()?.Id ?? 0;
            await LoadExpiringAsync();
        }, "در حال بارگذاری...");
    }

    partial void OnSelectedProductIdChanged(int value) => _ = LoadForProductAsync();

    private async Task LoadForProductAsync()
    {
        if (SelectedProductId <= 0) return;
        var batches = await _mediator.Send(new GetBatchesQuery(SelectedProductId));
        Batches.Clear(); foreach (var b in batches) Batches.Add(b);
        var serials = await _mediator.Send(new GetSerialsQuery(SelectedProductId));
        Serials.Clear(); foreach (var s in serials) Serials.Add(s);
    }

    [RelayCommand]
    private async Task LoadExpiringAsync()
    {
        var today = _calendar.GetCurrentPersianDate();
        var list = await _mediator.Send(new GetExpiringBatchesQuery(today, ExpiryHorizon));
        Expiring.Clear(); foreach (var e in list) Expiring.Add(e);
    }

    [RelayCommand]
    private async Task AddBatchAsync()
    {
        if (SelectedProductId <= 0) { await _dialogService.ShowWarningAsync("یک کالا انتخاب کنید."); return; }
        if (string.IsNullOrWhiteSpace(BatchNumber)) { await _dialogService.ShowWarningAsync("شمارهٔ بچ را وارد کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new SaveBatchCommand(SelectedProductId, BatchNumber.Trim(),
                NullIfEmpty(ProductionDate), NullIfEmpty(ExpiryDate), BatchQuantity,
                BatchPurchasePrice > 0 ? BatchPurchasePrice : null, null));
            if (r.Succeeded)
            {
                BatchNumber = ProductionDate = ExpiryDate = ""; BatchQuantity = BatchPurchasePrice = 0;
                await LoadForProductAsync(); await LoadExpiringAsync();
                await _dialogService.ShowSuccessAsync("بچ ثبت شد.");
            }
            else await _dialogService.ShowErrorAsync(r.ErrorMessage);
        }, "در حال ثبت بچ...");
    }

    [RelayCommand]
    private async Task AddSerialAsync()
    {
        if (SelectedProductId <= 0) { await _dialogService.ShowWarningAsync("یک کالا انتخاب کنید."); return; }
        if (string.IsNullOrWhiteSpace(SerialNumber)) { await _dialogService.ShowWarningAsync("شمارهٔ سریال را وارد کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new SaveSerialCommand(SelectedProductId, SerialNumber.Trim(), null,
                SerialPurchasePrice > 0 ? SerialPurchasePrice : null, _calendar.GetCurrentPersianDate()));
            if (r.Succeeded)
            {
                SerialNumber = ""; SerialPurchasePrice = 0;
                await LoadForProductAsync();
                await _dialogService.ShowSuccessAsync("سریال ثبت شد.");
            }
            else await _dialogService.ShowErrorAsync(r.ErrorMessage);
        }, "در حال ثبت سریال...");
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
