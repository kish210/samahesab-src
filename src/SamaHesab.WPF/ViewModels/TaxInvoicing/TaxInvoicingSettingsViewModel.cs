using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Modules.TaxInvoicing.Application.Commands;
using SamaHesab.Modules.TaxInvoicing.Application.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.TaxInvoicing;

/// <summary>
/// تنظیماتِ سامانهٔ مودیان — همان الگویِ <c>TourismSettingsViewModel</c>. مسیرِ فایلِ گواهیِ دیجیتال
/// از View (دیالوگِ انتخابِ فایل در code-behind) پر می‌شود؛ اینجا فقط رشتهٔ مسیر نگه‌داری می‌شود.
/// </summary>
public partial class TaxInvoicingSettingsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    [ObservableProperty] private string? _taxMemoryId;
    [ObservableProperty] private bool _useSandbox = true;
    [ObservableProperty] private string? _certificatePath;
    [ObservableProperty] private string? _certificatePassword;
    [ObservableProperty] private bool _enabled;

    public TaxInvoicingSettingsViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var s = await _mediator.Send(new GetModianSettingsQuery());
            TaxMemoryId = s.TaxMemoryId;
            UseSandbox = s.UseSandbox;
            CertificatePath = s.CertificatePath;
            CertificatePassword = s.CertificatePassword;
            Enabled = s.Enabled;
        }, "در حال بارگذاریِ تنظیماتِ مودیان...");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new SaveModianSettingsCommand(
                TaxMemoryId, UseSandbox, CertificatePath, CertificatePassword, Enabled));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync("تنظیماتِ سامانهٔ مودیان ذخیره شد.");
        }, "در حال ذخیره...");
    }
}
