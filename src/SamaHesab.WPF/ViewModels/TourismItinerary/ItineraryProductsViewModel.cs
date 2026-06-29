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
/// سانس‌بندیِ سفر — تعریفِ سانس‌های زمانیِ هر **محصولِ گردشگری** (همان محصولِ یکپارچه؛ TUR-UNIFY).
/// محصول این‌جا فقط برای انتخاب و نمایش است (تعریف/قیمت/تأمین‌کننده/پورسانت در «محصولاتِ گردشگری»).
/// سانس‌ها زمان‌بندیِ برنامه‌ریزِ اقامتی را ممکن می‌کنند.
/// </summary>
public partial class ItineraryProductsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    public ObservableCollection<ItineraryProductDto> Products { get; } = new();
    public ObservableCollection<ItineraryProductSessionDto> SelectedProductSessions { get; } = new();

    [ObservableProperty] private ItineraryProductDto? _selectedProduct;

    // فرمِ سانس (برای محصولِ انتخاب‌شده)
    [ObservableProperty] private string _sessionLabel = string.Empty;
    [ObservableProperty] private string _sessionStart = "09:00";
    [ObservableProperty] private string _sessionEnd = "12:00";
    [ObservableProperty] private int _sessionCapacity = 1;

    public ItineraryProductsViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override Task LoadAsync() => ReloadAsync();

    partial void OnSelectedProductChanged(ItineraryProductDto? value)
    {
        SelectedProductSessions.Clear();
        if (value is null) return;
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
        }, "در حال بارگذاری محصولاتِ گردشگری...");
    }

    [RelayCommand]
    private async Task AddSessionAsync()
    {
        if (SelectedProduct is not { } p) { await _dialogService.ShowErrorAsync("ابتدا یک محصول را انتخاب کنید."); return; }
        if (!TryParseMinute(SessionStart, out var start) || !TryParseMinute(SessionEnd, out var end))
        { await _dialogService.ShowErrorAsync("زمانِ سانس را به‌صورتِ HH:mm وارد کنید."); return; }

        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new SaveProductSessionCommand(0, p.Id, SessionLabel, start, end, SessionCapacity));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            SessionLabel = string.Empty;
            await ReloadAsync();
            SelectedProduct = Products.FirstOrDefault(x => x.Id == p.Id);
        }, "در حال افزودنِ سانس...");
    }

    [RelayCommand]
    private async Task DeleteSessionAsync(ItineraryProductSessionDto? session)
    {
        if (session is null || SelectedProduct is not { } p) return;
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new DeleteProductSessionCommand(session.Id));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await ReloadAsync();
            SelectedProduct = Products.FirstOrDefault(x => x.Id == p.Id);
        }, "در حال حذفِ سانس...");
    }

    /// <summary>«HH:mm» → دقیقه از نیمه‌شب (HH:mm:ss را هم می‌پذیرد).</summary>
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
