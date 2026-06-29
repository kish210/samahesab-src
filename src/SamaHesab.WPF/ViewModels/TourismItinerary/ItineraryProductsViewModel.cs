using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Modules.Tourism.Application.Itinerary.Commands;
using SamaHesab.Modules.Tourism.Application.Itinerary.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.TourismItinerary;

/// <summary>
/// مدیریتِ محصولاتِ اقامتی + سانس‌های زمانیِ هرکدام (بخشِ Operation). محصول: نام/قیمت/هزینه/ظرفیت؛
/// سود خالص محاسبه و نمایش داده می‌شود. سانس: برچسب + بازهٔ «HH:mm» (تبدیل به دقیقه) + ظرفیت.
/// </summary>
public partial class ItineraryProductsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    public ObservableCollection<ItineraryProductDto> Products { get; } = new();
    public ObservableCollection<ItineraryProductSessionDto> SelectedProductSessions { get; } = new();

    [ObservableProperty] private ItineraryProductDto? _selectedProduct;

    // فرمِ محصول
    [ObservableProperty] private int _editId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private decimal _salePrice;
    [ObservableProperty] private decimal _cost;
    [ObservableProperty] private int _capacity = 1;
    [ObservableProperty] private bool _active = true;
    public decimal NetProfitPreview => SalePrice - Cost;

    // فرمِ سانس (برای محصولِ انتخاب‌شده)
    [ObservableProperty] private string _sessionLabel = string.Empty;
    [ObservableProperty] private string _sessionStart = "09:00";
    [ObservableProperty] private string _sessionEnd = "12:00";
    [ObservableProperty] private int _sessionCapacity = 1;

    public ItineraryProductsViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override Task LoadAsync() => ReloadAsync();

    partial void OnSalePriceChanged(decimal v) => OnPropertyChanged(nameof(NetProfitPreview));
    partial void OnCostChanged(decimal v) => OnPropertyChanged(nameof(NetProfitPreview));

    partial void OnSelectedProductChanged(ItineraryProductDto? value)
    {
        SelectedProductSessions.Clear();
        if (value is null) return;
        EditId = value.Id; Name = value.Name; SalePrice = value.SalePrice; Cost = value.Cost;
        Capacity = value.Capacity; Active = value.Active;
        foreach (var s in value.Sessions) SelectedProductSessions.Add(s);
    }

    private async Task ReloadAsync()
    {
        await ExecuteAsync(async () =>
        {
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
        SelectedProductSessions.Clear();
    }

    [RelayCommand]
    private async Task SaveProductAsync()
    {
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new SaveItineraryProductCommand(EditId, Name, SalePrice, Cost, Capacity, null, Active));
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
        var parts = hhmm.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return false;
        if (h is < 0 or > 23 || m is < 0 or > 59) return false;
        minute = h * 60 + m;
        return true;
    }
}
