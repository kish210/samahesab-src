using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Support.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Support;

/// <summary>🆘 HC-4 — «درخواست‌های من»: نمای یکپارچهٔ محلیِ باگ/قابلیت/تیکت با وضعیت + همگام‌سازی.</summary>
public partial class MyRequestsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _pendingSyncCount;

    public ObservableCollection<MyRequestItem> Items { get; } = new();

    public MyRequestsViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ExecuteAsync(async () =>
        {
            var list = await _mediator.Send(new GetMyRequestsQuery());
            Items.Clear();
            foreach (var i in list) Items.Add(i);
            TotalCount = Items.Count;
            PendingSyncCount = Items.Count(i => i.SyncText is "در صفِ ارسال" or "ناموفق");
        }, "در حال بارگذاری...");
    }
}
