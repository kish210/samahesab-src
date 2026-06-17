using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Support;
using SamaHesab.Application.Support.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Support;

/// <summary>🆘 HC-5 — یادداشت‌های نسخه (کَش از وردپرس؛ آفلاین→کَش).</summary>
public partial class ReleaseNotesViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    [ObservableProperty] private ReleaseDto? _selected;
    [ObservableProperty] private string _statusText = string.Empty;

    public ObservableCollection<ReleaseDto> Releases { get; } = new();

    public ReleaseNotesViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService) { _mediator = mediator; }

    public override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ExecuteAsync(async () =>
        {
            var list = await _mediator.Send(new SyncReleaseNotesCommand());
            Releases.Clear();
            foreach (var r in list) Releases.Add(r);
            Selected = Releases.FirstOrDefault();
            StatusText = Releases.Count == 0
                ? "یادداشتِ نسخه‌ای موجود نیست. اگر سرورِ پشتیبانی پیکربندی شود، این فهرست از kishwifi.com پر می‌شود."
                : $"{Releases.Count} نسخه";
        }, "در حال دریافتِ یادداشت‌های نسخه...");
    }
}
