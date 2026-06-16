using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Dimensions;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Security.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Onboarding;

/// <summary>
/// فاز ۱۲ G3 — ویزاردِ راه‌اندازیِ اولیه (First-Run):
/// اطلاعاتِ شرکت/لوگو + سالِ مالی + اجبارِ تغییرِ رمزِ پیش‌فرضِ admin. یک‌بار در اولین اجرا.
/// از commandهای موجود استفاده می‌کند (SaveFiscalYear / ChangeUserPassword) + AppSettingsStore برای شرکت.
/// </summary>
public partial class FirstRunWizardViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _user;

    // شرکت
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private string? _companyPhone;
    [ObservableProperty] private string? _companyNationalId;
    [ObservableProperty] private string? _companyEconomicCode;
    [ObservableProperty] private string? _companyAddress;
    [ObservableProperty] private string? _logoPath;

    // سالِ مالی
    [ObservableProperty] private string _fiscalTitle = string.Empty;
    [ObservableProperty] private string _fiscalStart = string.Empty;
    [ObservableProperty] private string _fiscalEnd = string.Empty;

    // رمزِ ادمین
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;

    /// <summary>پنجره با این رویداد خود را می‌بندد (اتمام یا «بعداً»).</summary>
    public event System.Action? Finished;

    public FirstRunWizardViewModel(IMediator mediator, ICurrentUserService user,
        IPersianCalendarService calendar, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _user = user;

        // پیش‌پُر از تنظیماتِ موجود + پیشنهادِ سالِ مالیِ جاری.
        var g = AppSettingsStore.GetGeneral();
        CompanyName = g.CompanyName ?? string.Empty;
        CompanyPhone = g.CompanyPhone; CompanyNationalId = g.CompanyNationalId;
        CompanyEconomicCode = g.CompanyEconomicCode; CompanyAddress = g.CompanyAddress;
        LogoPath = g.CompanyLogoPath;

        var today = calendar.GetCurrentPersianDate();                 // "1405/03/26"
        var year = today.Length >= 4 ? today[..4] : "1405";
        FiscalTitle = $"سالِ مالی {year}";
        FiscalStart = g.FiscalYearStart ?? $"{year}/01/01";
        FiscalEnd = g.FiscalYearEnd ?? $"{year}/12/29";
    }

    public override Task LoadAsync() => Task.CompletedTask;

    /// <summary>«بعداً» — بدونِ علامت‌گذاریِ تکمیل؛ ویزارد در اجرای بعدی دوباره می‌آید.</summary>
    [RelayCommand]
    private void Skip() => Finished?.Invoke();

    [RelayCommand]
    private async Task FinishAsync()
    {
        if (string.IsNullOrWhiteSpace(CompanyName))
        { await _dialogService.ShowWarningAsync("نامِ شرکت الزامی است."); return; }

        // رمز: اختیاری ولی اگر وارد شد باید تأیید بخورد.
        var wantsPassword = !string.IsNullOrWhiteSpace(NewPassword);
        if (wantsPassword && NewPassword != ConfirmPassword)
        { await _dialogService.ShowWarningAsync("رمزِ عبور و تکرارِ آن یکسان نیستند."); return; }
        if (!wantsPassword &&
            !await _dialogService.ConfirmAsync("رمزِ پیش‌فرضِ admin تغییر نکرده — ادامه می‌دهید؟ (توصیه: تغییر دهید)"))
            return;

        await ExecuteAsync(async () =>
        {
            // ۱) شرکت → تنظیماتِ محلی (merge با موجود)
            var g = AppSettingsStore.GetGeneral();
            g.CompanyName = CompanyName; g.CompanyPhone = CompanyPhone;
            g.CompanyNationalId = CompanyNationalId; g.CompanyEconomicCode = CompanyEconomicCode;
            g.CompanyAddress = CompanyAddress; g.CompanyLogoPath = LogoPath;
            g.FiscalYearStart = FiscalStart; g.FiscalYearEnd = FiscalEnd;

            // ۲) سالِ مالی → DB (command موجود)
            var fy = await _mediator.Send(new SaveFiscalYearCommand(0, FiscalTitle, FiscalStart, FiscalEnd));
            if (!fy.Succeeded)
            { await _dialogService.ShowErrorAsync(fy.ErrorMessage ?? "خطا در ثبتِ سالِ مالی."); return; }

            // ۳) رمزِ ادمین (در صورتِ ورود)
            if (wantsPassword && _user.UserId is int uid)
            {
                var pr = await _mediator.Send(new ChangeUserPasswordCommand(uid, NewPassword));
                if (!pr.Succeeded)
                { await _dialogService.ShowErrorAsync(pr.ErrorMessage ?? "خطا در تغییرِ رمز."); return; }
            }

            // ۴) علامتِ تکمیل و ذخیره
            g.SetupCompleted = true;
            AppSettingsStore.SaveGeneral(g);

            await _dialogService.ShowSuccessAsync("راه‌اندازیِ اولیه کامل شد. خوش آمدید!");
            Finished?.Invoke();
        }, "در حال ذخیرهٔ راه‌اندازی...");
    }
}
