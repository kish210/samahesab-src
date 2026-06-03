using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Inventory;

public partial class WarehouseViewModel : BaseViewModel
{
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly ICurrentUserService _currentUser;

    [ObservableProperty] private int _totalCount;

    public ObservableCollection<WarehouseRow> Warehouses { get; } = new();

    public WarehouseViewModel(IWarehouseRepository warehouseRepo, ICurrentUserService currentUser,
        IDialogService d, INavigationService n) : base(d, n)
    { _warehouseRepo = warehouseRepo; _currentUser = currentUser; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var companyId = _currentUser.CompanyId ?? 1;
            var list = await _warehouseRepo.GetByCompanyAsync(companyId);
            Warehouses.Clear();
            foreach (var w in list)
                Warehouses.Add(new WarehouseRow(w.Id, w.Code, w.Name, w.ManagerName ?? "",
                    w.Address ?? "", w.IsDefault, w.IsActive));
            TotalCount = Warehouses.Count;
        }, "در حال بارگذاری انبارها...");
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();
}

public record WarehouseRow(int Id, string Code, string Name, string Manager, string Address, bool IsDefault, bool IsActive);
