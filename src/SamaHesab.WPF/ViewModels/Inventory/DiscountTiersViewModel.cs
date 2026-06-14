using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.DiscountTiers;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Inventory;

/// <summary>
/// کارِ ۷/U6 — مدیریتِ تخفیفِ پلکانیِ مقداری: برای هر کالا چند پله («مقدار ≥ X → Y٪»).
/// در فاکتور فروش، هنگام افزودنِ کالا بهترین پله خودکار اعمال می‌شود (اگر تخفیفِ دستی نباشد).
/// </summary>
public partial class DiscountTiersViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IProductRepository _products;
    private readonly ICurrentUserService _currentUser;

    public ObservableCollection<TierPick> Products { get; } = new();
    public ObservableCollection<TierRow> Tiers { get; } = new();

    [ObservableProperty] private int _selectedProductId;
    [ObservableProperty] private string _search = string.Empty;

    public DiscountTiersViewModel(IMediator mediator, IProductRepository products,
        ICurrentUserService currentUser, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _products = products; _currentUser = currentUser; }

    public override async Task LoadAsync() => await RunSearchAsync();

    [RelayCommand]
    private async Task RunSearchAsync()
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var list = await _products.SearchAsync(companyId, Search?.Trim() ?? "");
        Products.Clear();
        foreach (var p in list) Products.Add(new TierPick(p.Id, $"{p.Code} — {p.Name}"));
        if (Products.Count > 0 && SelectedProductId == 0) SelectedProductId = Products[0].Id;
    }

    partial void OnSelectedProductIdChanged(int value) => _ = LoadTiersAsync();

    private async Task LoadTiersAsync()
    {
        Tiers.Clear();
        if (SelectedProductId <= 0) return;
        foreach (var t in await _mediator.Send(new GetProductDiscountTiersQuery(SelectedProductId)))
            Tiers.Add(new TierRow { MinQty = t.MinQty, DiscountPercent = t.DiscountPercent });
    }

    [RelayCommand] private void AddTier() => Tiers.Add(new TierRow { MinQty = 0, DiscountPercent = 0 });
    [RelayCommand] private void RemoveTier(TierRow? r) { if (r != null) Tiers.Remove(r); }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedProductId <= 0) { await _dialogService.ShowWarningAsync("ابتدا کالا را انتخاب کنید."); return; }
        var tiers = Tiers.Where(t => t.MinQty > 0)
            .Select(t => new DiscountTierDto(t.MinQty, t.DiscountPercent)).ToList();
        var res = await _mediator.Send(new SaveProductDiscountTiersCommand(SelectedProductId, tiers));
        if (res.Succeeded) { await _dialogService.ShowSuccessAsync($"{tiers.Count} پلهٔ تخفیف ذخیره شد."); await LoadTiersAsync(); }
        else await _dialogService.ShowErrorAsync(res.ErrorMessage);
    }
}

public record TierPick(int Id, string Display);

public partial class TierRow : ObservableObject
{
    [ObservableProperty] private decimal _minQty;
    [ObservableProperty] private decimal _discountPercent;
}
