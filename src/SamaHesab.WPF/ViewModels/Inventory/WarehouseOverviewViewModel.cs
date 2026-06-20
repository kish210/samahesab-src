using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.BI.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>
/// UX-INV-1 — نمای انبار (Warehouse Overview): صفحهٔ عملیاتیِ انباردار.
/// نوارِ KPI + فهرستِ اقلامِ نیازمندِ توجه (زیرِ حداقل/ناموجود) با کسری و پیشنهادِ سفارش.
/// روی کوئری‌های آماده: GetWarehouseDashboardQuery + GetReorderReportQuery + GetInventoryValuationQuery.
/// </summary>
public partial class WarehouseOverviewViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private string _todayText = string.Empty;

    // ── KPI ──
    [ObservableProperty] private int _outOfStockCount;
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private int _reorderSuggestions;
    [ObservableProperty] private decimal _stockValue;
    [ObservableProperty] private int _itemCount;

    // ── فهرستِ نیازمندِ توجه ──
    public ObservableCollection<ReorderRow> AttentionItems { get; } = new();
    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private ReorderRow? _selectedItem;
    private List<ReorderRow> _allAttention = new();

    public WarehouseOverviewViewModel(IMediator mediator, IPersianCalendarService calendar,
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

            var val = await _mediator.Send(new GetInventoryValuationQuery());
            StockValue = val.TotalValue;
            ItemCount = val.ItemCount;

            var rr = await _mediator.Send(new GetReorderReportQuery());
            _allAttention = rr.Rows
                .Select(r => new ReorderRow(r.ProductId, r.Code, r.Name, r.OnHand, r.MinStock, r.Shortage, r.SuggestedQty))
                .ToList();
            ApplyFilter();
        }, "در حال بارگذاری نمای انبار...");
    }

    partial void OnSearchChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        AttentionItems.Clear();
        IEnumerable<ReorderRow> q = _allAttention;
        if (!string.IsNullOrWhiteSpace(Search))
            q = q.Where(r => r.Name.Contains(Search) || r.Code.Contains(Search));
        foreach (var r in q) AttentionItems.Add(r);
    }

    [RelayCommand] private Task RefreshAsync() => LoadAsync();
    [RelayCommand] private void OpenReorderReport() => _navigationService.NavigateTo("ReorderReport");
    [RelayCommand] private void OpenStockTransfer() => _navigationService.NavigateTo("StockTransfer");
    [RelayCommand] private void OpenKardex() => _navigationService.NavigateTo("Kardex");

    /// <summary>کاردکسِ کالای انتخاب‌شده (راست‌کلیک).</summary>
    [RelayCommand] private void ViewKardex(ReorderRow? r) { if (r != null) _navigationService.NavigateTo("Kardex"); }
}

/// <summary>ردیفِ کالای نیازمندِ توجه در نمای انبار.</summary>
public record ReorderRow(int ProductId, string Code, string Name, decimal OnHand,
    decimal MinStock, decimal Shortage, decimal SuggestedQty);
