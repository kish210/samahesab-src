using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Support.Commands;
using SamaHesab.Domain.Enums;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Support;

/// <summary>🆘 HC-4 — فرمِ درخواستِ قابلیتِ تازه.</summary>
public partial class FeatureRequestViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string? _businessBenefit;
    [ObservableProperty] private EnumOption<FeaturePriority> _selectedPriority;
    [ObservableProperty] private string? _resultMessage;
    [ObservableProperty] private bool _resultIsError;

    public bool HasResult => !string.IsNullOrEmpty(ResultMessage);
    partial void OnResultMessageChanged(string? value) => OnPropertyChanged(nameof(HasResult));

    public ObservableCollection<EnumOption<FeaturePriority>> Priorities { get; } = new()
    {
        new("کم", FeaturePriority.Low), new("متوسط", FeaturePriority.Medium), new("زیاد", FeaturePriority.High),
    };

    public FeatureRequestViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator;
        _selectedPriority = Priorities[1];
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Title)) { await _dialogService.ShowWarningAsync("عنوانِ درخواست را وارد کنید."); return; }
        if (string.IsNullOrWhiteSpace(Description)) { await _dialogService.ShowWarningAsync("شرحِ درخواست را وارد کنید."); return; }

        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new CreateFeatureRequestCommand(
                Title.Trim(), Description.Trim(), BusinessBenefit, SelectedPriority.Value, null));
            ResultIsError = !res.Succeeded;
            ResultMessage = res.Succeeded ? res.Value!.Message : res.ErrorMessage;
            if (res.Succeeded) { Title = string.Empty; Description = string.Empty; BusinessBenefit = null; }
        }, "در حال ثبتِ درخواست...");
    }
}
