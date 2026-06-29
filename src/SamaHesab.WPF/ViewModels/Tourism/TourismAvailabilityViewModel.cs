using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Modules.Tourism.Application;
using SamaHesab.Modules.Tourism.Application.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Tourism;

/// <summary>
/// نمای موجودی/ظرفیتِ محصولاتِ گردشگری برای فروشنده (TUR-C1-availability؛ بک‌اندِ pc).
/// هر ردیف: قیمت + ظرفیتِ کل + فروش‌رفته + ماندهٔ ظرفیت + علامتِ تمام‌شده (ظرفیتِ null = نامحدود).
/// </summary>
public partial class TourismAvailabilityViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    public ObservableCollection<TourismAvailabilityRow> Rows { get; } = new();
    public int SoldOutCount => Rows.Count(r => r.IsSoldOut);

    public TourismAvailabilityViewModel(IMediator mediator,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ExecuteAsync(async () =>
        {
            Rows.Clear();
            foreach (var r in await _mediator.Send(new GetTourismAvailabilityQuery(OnlyActive: true)))
                Rows.Add(r);
            OnPropertyChanged(nameof(SoldOutCount));
        }, "در حال بارگذاری موجودی...");
    }
}
