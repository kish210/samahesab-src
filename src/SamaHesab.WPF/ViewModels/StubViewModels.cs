using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Inventory
{
    public partial class StockAdjustViewModel : BaseViewModel
    {
        private readonly IProductRepository _productRepo;
        private readonly IWarehouseRepository _warehouseRepo;
        private readonly IStockItemRepository _stockRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _user;

        [ObservableProperty] private int _selectedWarehouseId;
        private bool _suppressReload;

        public ObservableCollection<StockAdjustRow> Rows { get; } = new();
        public List<WarehouseOption> Warehouses { get; private set; } = new();

        public StockAdjustViewModel(IProductRepository productRepo, IWarehouseRepository warehouseRepo,
            IStockItemRepository stockRepo, IUnitOfWork unitOfWork, ICurrentUserService user,
            IDialogService d, INavigationService n) : base(d, n)
        { _productRepo = productRepo; _warehouseRepo = warehouseRepo; _stockRepo = stockRepo;
          _unitOfWork = unitOfWork; _user = user; }

        public override async Task LoadAsync()
        {
            await ExecuteAsync(async () =>
            {
                var companyId = _user.CompanyId ?? 1;
                Warehouses = (await _warehouseRepo.GetByCompanyAsync(companyId))
                    .Select(w => new WarehouseOption(w.Id, w.Name)).ToList();
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
            var companyId = _user.CompanyId ?? 1;
            var products = await _productRepo.SearchAsync(companyId, "");
            Rows.Clear();
            foreach (var p in products)
            {
                var stock = await _stockRepo.GetByProductAndWarehouseAsync(p.Id, SelectedWarehouseId);
                var cur = stock?.Quantity ?? 0;
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
                try
                {
                    foreach (var r in changed)
                    {
                        var stock = await _stockRepo.GetByProductAndWarehouseAsync(r.ProductId, SelectedWarehouseId);
                        if (stock == null)
                        {
                            stock = StockItem.Create(r.ProductId, SelectedWarehouseId);
                            await _stockRepo.AddAsync(stock);
                        }
                        var diff = r.NewQty - r.CurrentQty;
                        if (diff > 0) stock.AddStock(diff, r.UnitCost);
                        else stock.RemoveStock(-diff);
                        _stockRepo.Update(stock);
                    }
                    await _unitOfWork.SaveChangesAsync();
                    await _dialogService.ShowSuccessAsync("تعدیل موجودی ذخیره شد.");
                    await LoadRowsAsync();
                }
                catch (Exception ex) { await _dialogService.ShowErrorAsync("خطا: " + ex.Message); }
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
