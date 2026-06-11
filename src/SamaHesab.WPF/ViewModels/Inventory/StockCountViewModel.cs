using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>کار #۲۸ — انبارگردانی سندی: شروع شمارش، ثبت تعداد شمرده‌شده، و قطعی‌سازی (تبدیل مغایرت به تعدیل).</summary>
public partial class StockCountViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly IPersianCalendarService _calendar;

    public List<ApiWarehouse> Warehouses { get; private set; } = new();
    public ObservableCollection<StockCountRow> Lines { get; } = new();

    [ObservableProperty] private int _selectedWarehouseId;
    [ObservableProperty] private int _sessionId;
    [ObservableProperty] private string _date = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private int _varianceCount;
    [ObservableProperty] private bool _isStarted;
    [ObservableProperty] private bool _isPosted;
    [ObservableProperty] private string _resultText = string.Empty;

    public StockCountViewModel(ApiClient api, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _api = api; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        Date = _calendar.GetCurrentPersianDate();
        Warehouses = await _api.GetWarehousesAsync();
        OnPropertyChanged(nameof(Warehouses));
        if (Warehouses.Count > 0) SelectedWarehouseId = Warehouses[0].Id;
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (SelectedWarehouseId == 0) { await _dialogService.ShowErrorAsync("انبار را انتخاب کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var (ok, sessionId, error) = await _api.StartStockCountAsync(SelectedWarehouseId, Date);
            if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در شروع انبارگردانی."); return; }
            SessionId = sessionId; IsStarted = true; IsPosted = false; ResultText = string.Empty;
            await ReloadAsync();
        }, "در حال شروع انبارگردانی...");
    }

    private async Task ReloadAsync()
    {
        var dto = await _api.GetStockCountAsync(SessionId);
        Lines.Clear();
        if (dto is null) return;
        Status = dto.Status; VarianceCount = dto.VarianceCount;
        foreach (var l in dto.Lines)
            Lines.Add(new StockCountRow(l.ProductId, l.ProductName, l.SystemQty) { CountedQty = l.CountedQty });
    }

    /// <summary>ثبت تعداد شمرده‌شده‌ی همه‌ی ردیف‌ها روی سرور و بازخوانی مغایرت‌ها.</summary>
    [RelayCommand]
    private async Task SaveCountsAsync()
    {
        if (!IsStarted) return;
        await ExecuteAsync(async () =>
        {
            foreach (var row in Lines)
            {
                var (ok, error) = await _api.SetStockCountLineAsync(SessionId, row.ProductId, row.CountedQty);
                if (!ok) { await _dialogService.ShowErrorAsync($"{row.ProductName}: {error}"); return; }
            }
            await ReloadAsync();
            await _dialogService.ShowSuccessAsync("شمارش‌ها ثبت شد.");
        }, "در حال ثبت شمارش‌ها...");
    }

    [RelayCommand]
    private async Task PostAsync()
    {
        if (!IsStarted || IsPosted) return;
        var ok2 = await _dialogService.ConfirmAsync("قطعی‌سازی انبارگردانی؟ مغایرت‌ها به تعدیل موجودی و سند تبدیل می‌شوند.", "قطعی‌سازی");
        if (!ok2) return;
        await ExecuteAsync(async () =>
        {
            // ابتدا شمارش‌های جاری ثبت شوند تا قطعی‌سازی روی آخرین داده باشد
            foreach (var row in Lines)
                await _api.SetStockCountLineAsync(SessionId, row.ProductId, row.CountedQty);
            var (ok, adjusted, variance, error) = await _api.PostStockCountAsync(SessionId);
            if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در قطعی‌سازی."); return; }
            IsPosted = true;
            ResultText = $"قطعی شد — {adjusted} قلم تعدیل، مجموع مغایرت {variance:N0}";
            await ReloadAsync();
        }, "در حال قطعی‌سازی...");
    }
}

public partial class StockCountRow : ObservableObject
{
    public int ProductId { get; }
    public string ProductName { get; }
    public decimal SystemQty { get; }
    [ObservableProperty] private decimal _countedQty;
    public decimal Variance => CountedQty - SystemQty;

    public StockCountRow(int productId, string name, decimal systemQty)
    { ProductId = productId; ProductName = name; SystemQty = systemQty; }

    partial void OnCountedQtyChanged(decimal value) => OnPropertyChanged(nameof(Variance));
}
