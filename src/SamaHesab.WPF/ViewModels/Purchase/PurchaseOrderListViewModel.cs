using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Automation;          // ReorderSuggestion
using SamaHesab.Application.Automation.Queries;   // GetReorderSuggestionsQuery
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Purchase.Commands;     // CreatePurchaseOrderFromReorderCommand
using SamaHesab.Application.Purchase.Queries;      // GetPurchaseOrdersQuery / PurchaseOrderDto
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Purchase;

/// <summary>
/// F9-2 — سفارش‌های خرید + پیشنهادِ نقطهٔ سفارش: فهرستِ سفارش‌ها (`GetPurchaseOrdersQuery`) +
/// پیشنهادهای reorder (`GetReorderSuggestionsQuery`) + ساختِ سفارش از پیشنهاد
/// (`CreatePurchaseOrderFromReorderCommand`ِ آماده). لِینِ خریدِ C2 (UIِ نبوده).
/// </summary>
public partial class PurchaseOrderListViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    public ObservableCollection<PurchaseOrderDto> Orders { get; } = new();
    public ObservableCollection<ReorderSuggestion> Suggestions { get; } = new();

    [ObservableProperty] private int _orderCount;
    [ObservableProperty] private decimal _ordersTotal;
    [ObservableProperty] private int _suggestionCount;

    public PurchaseOrderListViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            Orders.Clear();
            foreach (var o in await _mediator.Send(new GetPurchaseOrdersQuery())) Orders.Add(o);
            OrderCount = Orders.Count;
            OrdersTotal = Orders.Sum(o => o.Total);

            Suggestions.Clear();
            foreach (var s in await _mediator.Send(new GetReorderSuggestionsQuery())) Suggestions.Add(s);
            SuggestionCount = Suggestions.Count;
        }, "در حال بارگذاری سفارش‌های خرید...");
    }

    /// <summary>ساختِ یک سفارشِ خریدِ خودکار از همهٔ کالاهای زیرِ نقطهٔ سفارش.</summary>
    [RelayCommand]
    private async Task CreateFromReorderAsync()
    {
        if (Suggestions.Count == 0) { await _dialogService.ShowWarningAsync("کالایی زیرِ نقطهٔ سفارش نیست."); return; }
        if (!await _dialogService.ConfirmAsync($"برای {Suggestions.Count} کالای زیرِ نقطهٔ سفارش، یک سفارشِ خرید ساخته شود؟")) return;
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new CreatePurchaseOrderFromReorderCommand(_calendar.GetCurrentPersianDate()));
            if (!r.Succeeded) { await _dialogService.ShowErrorAsync(r.ErrorMessage ?? "خطا در ساختِ سفارش."); return; }
            await _dialogService.ShowSuccessAsync($"سفارشِ خرید #{r.Value} از پیشنهادِ نقطهٔ سفارش ساخته شد.");
            await LoadAsync();
        }, "در حال ساختِ سفارش خرید...");
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();
}
