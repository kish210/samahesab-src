using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.BI.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>
/// F9-9 — داشبورد انباردار: وضعیتِ موجودی روی `GetWarehouseDashboardQuery`ِ آماده
/// (کالاهای ناموجود · زیرِ حداقل · پیشنهادِ سفارش). سومین داشبوردِ نقش‌محورِ C1.
/// </summary>
public partial class WarehouseDashboardViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private string _todayText = string.Empty;
    [ObservableProperty] private int _outOfStockCount;
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private int _reorderSuggestions;

    public WarehouseDashboardViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var today = _calendar.GetCurrentPersianDate();
            TodayText = today;
            var d = await _mediator.Send(new GetWarehouseDashboardQuery(today));
            OutOfStockCount = d.OutOfStockCount;
            LowStockCount = d.LowStockCount;
            ReorderSuggestions = d.ReorderSuggestions;
        }, "در حال بارگذاری داشبورد انبار...");
    }

    [RelayCommand] private Task RefreshAsync() => LoadAsync();
    [RelayCommand] private void OpenReorder() => _navigationService.NavigateTo("ReorderReport");
    [RelayCommand] private void OpenProducts() => _navigationService.NavigateTo("Products");
}
