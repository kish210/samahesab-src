using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Modules.Tourism.Application.Itinerary;
using SamaHesab.Modules.Tourism.Application.Itinerary.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.TourismItinerary;

/// <summary>
/// برنامه‌ریزِ اقامتی (بخشِ Operation): با الگوریتمِ هوشمند یک برنامهٔ پیشنهادی برای مهمان تولید می‌کند
/// (اولویتِ سود، عدمِ تداخلِ سانس، تنوع)، اقلام را گروه‌بندیِ روزانه نشان می‌دهد و لینکِ پنلِ مهمان را می‌سازد.
/// </summary>
public partial class ItineraryPlannerViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    /// <summary>پایهٔ آدرسِ پنلِ مهمان (سرورِ API + مسیرِ صفحهٔ مهمان). نیازمندِ اجرای سرور.</summary>
    public const string PortalBaseUrl = "http://localhost:5080/itinerary/";

    [ObservableProperty] private string _guestName = string.Empty;
    [ObservableProperty] private int _days = 3;
    [ObservableProperty] private int _maxPerDay;          // ۰ = نامحدود
    [ObservableProperty] private bool _preferVariety = true;

    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private string _resultToken = string.Empty;
    [ObservableProperty] private string _portalLink = string.Empty;
    [ObservableProperty] private decimal _totalSale;
    [ObservableProperty] private decimal _totalProfit;
    [ObservableProperty] private int _stopCount;

    /// <summary>اقلامِ برنامهٔ تولیدشده برای نمایش (گروه‌بندیِ روزانه از طریقِ DayNumber).</summary>
    public ObservableCollection<GuestStopDto> Stops { get; } = new();

    public ItineraryPlannerViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override Task LoadAsync() => Task.CompletedTask;

    [RelayCommand]
    private async Task GenerateAsync()
    {
        await ExecuteAsync(async () =>
        {
            int? maxPerDay = MaxPerDay > 0 ? MaxPerDay : null;
            var res = await _mediator.Send(new GenerateItineraryCommand(GuestName, Days, null, maxPerDay, PreferVariety));
            if (!res.Succeeded) { HasResult = false; await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }

            var g = res.Value!;
            ResultToken = g.Token;
            PortalLink = PortalBaseUrl + g.Token;
            TotalSale = g.TotalSale; TotalProfit = g.TotalProfit; StopCount = g.StopCount;

            // بارگذاریِ اقلامِ کامل برای نمایش (همان دادهٔ پنلِ مهمان).
            Stops.Clear();
            var detail = await _mediator.Send(new GetGuestItineraryQuery(g.Token));
            if (detail.Succeeded && detail.Value is not null)
                foreach (var s in detail.Value.Stops) Stops.Add(s);

            HasResult = true;
            await _dialogService.ShowSuccessAsync($"برنامهٔ {StopCount} موردی ساخته شد. لینکِ پنلِ مهمان آماده است.");
        }, "در حال تولیدِ برنامهٔ پیشنهادی...");
    }

    [RelayCommand]
    private void CopyLink()
    {
        if (string.IsNullOrWhiteSpace(PortalLink)) return;
        try { System.Windows.Clipboard.SetText(PortalLink); } catch { /* کلیپ‌بورد در دسترس نیست */ }
    }
}
