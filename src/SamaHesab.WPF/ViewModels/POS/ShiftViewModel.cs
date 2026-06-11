using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.POS;

/// <summary>کار #۳۰/#۳۱ — شیفت/صندوق POS: باز/بستن صندوق + گزارش X (زنده) و Z (پایان شیفت).</summary>
public partial class ShiftViewModel : BaseViewModel
{
    private readonly ApiClient _api;

    [ObservableProperty] private bool _hasOpenShift;
    [ObservableProperty] private int _shiftId;
    [ObservableProperty] private decimal _openingFloat;
    [ObservableProperty] private decimal _cashSales;
    [ObservableProperty] private decimal _cardSales;
    [ObservableProperty] private int _salesCount;
    [ObservableProperty] private decimal _expectedCash;
    [ObservableProperty] private decimal _totalSales;

    // ورودی‌ها
    [ObservableProperty] private decimal _newFloat;
    [ObservableProperty] private decimal _countedCash;

    // نتیجه‌ی بستن (Z)
    [ObservableProperty] private bool _showClosedSummary;
    [ObservableProperty] private decimal _closedVariance;
    [ObservableProperty] private string _varianceLabel = string.Empty;

    public ShiftViewModel(ApiClient api, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _api = api; }

    public override async Task LoadAsync() => await RefreshAsync();

    /// <summary>گزارش X زنده (بدون بستن شیفت).</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        var s = await _api.GetCurrentShiftAsync();
        if (s is null) { HasOpenShift = false; return; }
        ShiftId = s.Id; OpeningFloat = s.OpeningFloat;
        CashSales = s.CashSales; CardSales = s.CardSales;
        SalesCount = s.SalesCount; ExpectedCash = s.ExpectedCash;
        TotalSales = s.CashSales + s.CardSales;
        HasOpenShift = true;
    }

    [RelayCommand]
    private async Task OpenShiftAsync()
    {
        if (HasOpenShift) { await _dialogService.ShowWarningAsync("یک شیفت باز هم‌اکنون وجود دارد."); return; }
        await ExecuteAsync(async () =>
        {
            var (ok, error) = await _api.OpenShiftAsync(NewFloat);
            if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در باز کردن صندوق."); return; }
            ShowClosedSummary = false;
            await RefreshAsync();
            await _dialogService.ShowSuccessAsync("صندوق باز شد.");
        }, "در حال باز کردن صندوق...");
    }

    [RelayCommand]
    private async Task CloseShiftAsync()
    {
        if (!HasOpenShift) { await _dialogService.ShowWarningAsync("شیفت بازی برای بستن وجود ندارد."); return; }
        var ok2 = await _dialogService.ConfirmAsync($"بستن صندوق با شمارش نقد {CountedCash:N0} ریال؟", "بستن صندوق (Z)");
        if (!ok2) return;
        await ExecuteAsync(async () =>
        {
            var (ok, summary, error) = await _api.CloseShiftAsync(CountedCash);
            if (!ok || summary is null) { await _dialogService.ShowErrorAsync(error ?? "خطا در بستن صندوق."); return; }
            ClosedVariance = summary.Variance;
            VarianceLabel = summary.Variance == 0 ? "بدون مغایرت"
                : summary.Variance > 0 ? $"اضافه: {summary.Variance:N0}" : $"کسری: {-summary.Variance:N0}";
            ShowClosedSummary = true;
            HasOpenShift = false;
            CountedCash = 0; NewFloat = 0;
        }, "در حال بستن صندوق...");
    }
}
