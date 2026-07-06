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

namespace SamaHesab.WPF.ViewModels.Inventory
{
    /// <summary>تعدیل موجودی — 🏛️ الگوی API-only: کلاینت→API، دسکتاپ→Application. بدونِ ریپازیتوریِ مستقیم.</summary>
    public partial class StockAdjustViewModel : BaseViewModel
    {
        private readonly IMediator _mediator;
        private readonly ApiClient _api;
        private readonly IPersianCalendarService _calendar;

        [ObservableProperty] private int _selectedWarehouseId;
        private bool _suppressReload;

        public ObservableCollection<StockAdjustRow> Rows { get; } = new();
        public List<WarehouseOption> Warehouses { get; private set; } = new();

        public StockAdjustViewModel(IMediator mediator, ApiClient api, IPersianCalendarService calendar,
            IDialogService d, INavigationService n) : base(d, n)
        { _mediator = mediator; _api = api; _calendar = calendar; }

        public override async Task LoadAsync()
        {
            await ExecuteAsync(async () =>
            {
                Warehouses = !string.IsNullOrWhiteSpace(_api.BaseUrl)
                    ? (await _api.GetWarehousesAsync()).Select(w => new WarehouseOption(w.Id, w.Name)).ToList()
                    : (await _mediator.Send(new GetWarehousesQuery())).Select(w => new WarehouseOption(w.Id, w.Name)).ToList();
                OnPropertyChanged(nameof(Warehouses));
                _suppressReload = true;
                if (SelectedWarehouseId == 0 && Warehouses.Count > 0)
                    SelectedWarehouseId = Warehouses[0].Id;
                _suppressReload = false;
                await LoadRowsAsync();
            }, "در حال بارگذاری موجودی...");
        }

        [RelayCommand]
        private async Task LoadRowsAsync()
        {
            var online = !string.IsNullOrWhiteSpace(_api.BaseUrl);
            // موجودیِ فعلیِ این انبار (productId→qty)
            var onHand = (online
                ? (await _api.GetWarehouseStockAsync(SelectedWarehouseId)).Select(s => (s.ProductId, s.Quantity))
                : (await _mediator.Send(new GetWarehouseStockQuery(SelectedWarehouseId))).Select(s => (s.ProductId, s.Quantity)))
                .GroupBy(s => s.ProductId).ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity));

            var products = online
                ? (await _api.GetProductListAsync()).Select(p => (p.Id, p.Code, p.Name, p.PurchasePrice))
                : (await _mediator.Send(new GetProductsQuery())).Select(p => (p.Id, p.Code, p.Name, p.PurchasePrice));

            Rows.Clear();
            foreach (var p in products)
            {
                var cur = onHand.TryGetValue(p.Id, out var q) ? q : 0;
                Rows.Add(new StockAdjustRow { ProductId = p.Id, ProductCode = p.Code,
                    ProductName = p.Name, CurrentQty = cur, NewQty = cur, UnitCost = p.PurchasePrice });
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (SelectedWarehouseId == 0) { await _dialogService.ShowErrorAsync("انبار را انتخاب کنید."); return; }
            var changed = Rows.Where(r => r.NewQty != r.CurrentQty).ToList();
            if (changed.Count == 0) { await _dialogService.ShowInfoAsync("تغییری برای ذخیره وجود ندارد."); return; }
            if (!await _dialogService.ConfirmAsync($"{changed.Count} ردیف تعدیل شود؟")) return;

            await ExecuteAsync(async () =>
            {
                var date = _calendar.GetCurrentPersianDate();
                var online = !string.IsNullOrWhiteSpace(_api.BaseUrl);
                foreach (var r in changed)
                {
                    var reason = string.IsNullOrWhiteSpace(r.Notes) ? "تعدیل موجودی" : r.Notes;
                    // 🏛️ تعدیل از طریقِ کامند/endpoint — نه ریپازیتوریِ مستقیم.
                    if (online)
                    {
                        var (ok, err) = await _api.AdjustStockAsync(SelectedWarehouseId, r.ProductId, r.NewQty, date, reason);
                        if (!ok) { await _dialogService.ShowErrorAsync("خطا: " + err); return; }
                    }
                    else
                    {
                        var res = await _mediator.Send(new AdjustStockCommand(SelectedWarehouseId, r.ProductId, r.NewQty, date, reason));
                        if (!res.Succeeded) { await _dialogService.ShowErrorAsync("خطا: " + res.ErrorMessage); return; }
                    }
                }
                await _dialogService.ShowSuccessAsync("تعدیل موجودی ذخیره شد.");
                await LoadRowsAsync();
            }, "در حال ذخیره...");
        }

        partial void OnSelectedWarehouseIdChanged(int value) { if (!_suppressReload) _ = LoadRowsAsync(); }
    }

    public partial class StockAdjustRow : ObservableObject
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal UnitCost { get; set; }
        [ObservableProperty] private decimal _currentQty;
        [ObservableProperty] private decimal _newQty;
        [ObservableProperty] private string? _notes;
    }

    public record WarehouseOption(int Id, string Name);
}
