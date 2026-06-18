using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>انبارها — 🏛️ الگوی API-only: کلاینت→API، دسکتاپ→Application. بدونِ ریپازیتوریِ مستقیم.</summary>
public partial class WarehouseViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ApiClient _api;

    [ObservableProperty] private int _totalCount;

    public ObservableCollection<WarehouseRow> Warehouses { get; } = new();

    public WarehouseViewModel(IMediator mediator, ApiClient api,
        IDialogService d, INavigationService n) : base(d, n)
    { _mediator = mediator; _api = api; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            Warehouses.Clear();
            if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
            {
                foreach (var w in await _api.GetWarehouseListAsync())
                    Warehouses.Add(new WarehouseRow(w.Id, w.Code, w.Name, w.Manager, w.Address, w.IsDefault, w.IsActive));
            }
            else
            {
                foreach (var w in await _mediator.Send(new GetWarehouseListQuery()))
                    Warehouses.Add(new WarehouseRow(w.Id, w.Code, w.Name, w.Manager, w.Address, w.IsDefault, w.IsActive));
            }
            TotalCount = Warehouses.Count;
        }, "در حال بارگذاری انبارها...");
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();
}

public record WarehouseRow(int Id, string Code, string Name, string Manager, string Address, bool IsDefault, bool IsActive);
