using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Modules.Tourism.Application.Itinerary.Commands;
using SamaHesab.Modules.Tourism.Application.Itinerary.Queries;
using SamaHesab.Modules.Tourism.Domain;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.TourismItinerary;

/// <summary>گزینهٔ مبنای پورسانت برای کمبوی فرم.</summary>
public record CommissionBasisOption(int Value, string Label);

/// <summary>
/// مدیریتِ محصولاتِ اقامتی + سانس‌های زمانی (بخشِ Operation). هر محصول: نام/قیمت/هزینه/ظرفیت +
/// **تأمین‌کننده** (شخص که محصول از او خریداری می‌شود) + **پورسانتِ بازاریاب** (مبلغ/درصدِ فروش/درصدِ سود).
/// سود خالص و مبلغِ پورسانت زنده محاسبه می‌شوند.
/// </summary>
public partial class ItineraryProductsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    public ObservableCollection<ItineraryProductDto> Products { get; } = new();
    public ObservableCollection<ItineraryProductSessionDto> SelectedProductSessions { get; } = new();
    public ObservableCollection<SupplierRowDto> Suppliers { get; } = new();

    /// <summary>مبناهای پورسانت (مطابقِ enum گردشگری: ۰=مبلغ ۱=٪فروش ۲=٪سود).</summary>
    public ObservableCollection<CommissionBasisOption> CommissionBasisOptions { get; } = new()
    {
        new(0, "مبلغِ ثابت (هر واحد)"),
        new(1, "درصدی از فروش"),
        new(2, "درصدی از سود"),
    };

    [ObservableProperty] private ItineraryProductDto? _selectedProduct;

    // فرمِ محصول
    [ObservableProperty] private int _editId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private int _supplierPartyId;          // ۰ = بدونِ تأمین‌کننده
    [ObservableProperty] private decimal _salePrice;
    [ObservableProperty] private decimal _cost;
    [ObservableProperty] private int _capacity = 1;
    [ObservableProperty] private bool _active = true;
    [ObservableProperty] private int _commissionBasis = 2;      // پیش‌فرض: درصدِ سود
    [ObservableProperty] private decimal _commissionValue;
    public decimal NetProfitPreview => SalePrice - Cost;

    /// <summary>مبلغِ پورسانتِ بازاریاب بر اساسِ مبنا + مقدار (نمایشِ زنده).</summary>
    public decimal CommissionPreview => CommissionBasis switch
    {
        0 => CommissionValue,
        1 => SalePrice * CommissionValue / 100m,
        2 => (SalePrice - Cost) * CommissionValue / 100m,
        _ => 0m
    };

    // فرمِ سانس (برای محصولِ انتخاب‌شده)
    [ObservableProperty] private string _sessionLabel = string.Empty;
    [ObservableProperty] private string _sessionStart = "09:00";
    [ObservableProperty] private string _sessionEnd = "12:00";
    [ObservableProperty] private int _sessionCapacity = 1;

    public ItineraryProductsViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override Task LoadAsync() => ReloadAsync();

    partial void OnSalePriceChanged(decimal v) { OnPropertyChanged(nameof(NetProfitPreview)); OnPropertyChanged(nameof(CommissionPreview)); }
    partial void OnCostChanged(decimal v) { OnPropertyChanged(nameof(NetProfitPreview)); OnPropertyChanged(nameof(CommissionPreview)); }
    partial void OnCommissionBasisChanged(int v) => OnPropertyChanged(nameof(CommissionPreview));
    partial void OnCommissionValueChanged(decimal v) => OnPropertyChanged(nameof(CommissionPreview));

    partial void OnSelectedProductChanged(ItineraryProductDto? value)
    {
        SelectedProductSessions.Clear();
        if (value is null) return;
        EditId = value.Id; Name = value.Name; SalePrice = value.SalePrice; Cost = value.Cost;
        Capacity = value.Capacity; Active = value.Active;
        SupplierPartyId = value.SupplierPartyId ?? 0;
        CommissionBasis = (int)value.MarketerCommissionBasis;
        CommissionValue = value.MarketerCommissionValue;
        foreach (var s in value.Sessions) SelectedProductSessions.Add(s);
    }

    private async Task ReloadAsync()
    {
        await ExecuteAsync(async () =>
        {
            if (Suppliers.Count == 0)
            {
                Suppliers.Add(new SupplierRowDto(0, "", "— بدونِ تأمین‌کننده —", "", "", 0, true));
                foreach (var s in await _mediator.Send(new GetSuppliersQuery())) Suppliers.Add(s);
            }
            var keep = SelectedProduct?.Id;
            Products.Clear();
            foreach (var p in await _mediator.Send(new GetItineraryProductsQuery(ActiveOnly: false))) Products.Add(p);
            SelectedProduct = Products.FirstOrDefault(p => p.Id == keep) ?? Products.FirstOrDefault();
        }, "در حال بارگذاری محصولاتِ اقامتی...");
    }

    [RelayCommand]
    private void NewProduct()
    {
        SelectedProduct = null;
        EditId = 0; Name = string.Empty; SalePrice = 0; Cost = 0; Capacity = 1; Active = true;
        SupplierPartyId = 0; CommissionBasis = 2; CommissionValue = 0;
        SelectedProductSessions.Clear();
    }

    [RelayCommand]
    private async Task SaveProductAsync()
    {
        await ExecuteAsync(async () =>
        {
            int? supplier = SupplierPartyId > 0 ? SupplierPartyId : null;
            var res = await _mediator.Send(new SaveItineraryProductCommand(
                EditId, Name, SalePrice, Cost, Capacity, supplier, Active,
                (CommissionBasis)CommissionBasis, CommissionValue));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync("محصول ذخیره شد.");
            EditId = res.Value;
            await ReloadAsync();
            SelectedProduct = Products.FirstOrDefault(p => p.Id == res.Value);
        }, "در حال ذخیره...");
    }

    [RelayCommand]
    private async Task DeleteProductAsync()
    {
        if (SelectedProduct is not { } p) { await _dialogService.ShowErrorAsync("ابتدا محصولی را انتخاب کنید."); return; }
        if (!await _dialogService.ConfirmAsync($"غیرفعال‌سازیِ «{p.Name}»؟ (دادهٔ تاریخی حفظ می‌شود)")) return;
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new DeleteItineraryProductCommand(p.Id));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await ReloadAsync();
        }, "در حال غیرفعال‌سازی...");
    }

    [RelayCommand]
    private async Task AddSessionAsync()
    {
        if (EditId <= 0) { await _dialogService.ShowErrorAsync("ابتدا محصول را ذخیره کنید."); return; }
        if (!TryParseMinute(SessionStart, out var start) || !TryParseMinute(SessionEnd, out var end))
        { await _dialogService.ShowErrorAsync("زمانِ سانس را به‌صورتِ HH:mm وارد کنید."); return; }

        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new SaveProductSessionCommand(0, EditId, SessionLabel, start, end, SessionCapacity));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            SessionLabel = string.Empty;
            await ReloadAsync();
            SelectedProduct = Products.FirstOrDefault(p => p.Id == EditId);
        }, "در حال افزودنِ سانس...");
    }

    /// <summary>«HH:mm» → دقیقه از نیمه‌شب.</summary>
    private static bool TryParseMinute(string? hhmm, out int minute)
    {
        minute = 0;
        if (string.IsNullOrWhiteSpace(hhmm)) return false;
        var parts = hhmm.Trim().Split(':');
        if (parts.Length < 2 || !int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return false;
        if (h is < 0 or > 23 || m is < 0 or > 59) return false;
        minute = h * 60 + m;
        return true;
    }
}
