using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Support.Commands;
using SamaHesab.Domain.Enums;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Support;

/// <summary>گزینهٔ نمایشیِ یک enum (برچسبِ فارسی + مقدار).</summary>
public sealed record EnumOption<T>(string Label, T Value);

/// <summary>🆘 HC-3b — زمینهٔ استثناء برای پرکردنِ خودکارِ فرمِ گزارشِ باگ.</summary>
public sealed record ExceptionContext(string Message, string StackTrace, string Screen);

/// <summary>
/// 🆘 HC-3 — فرمِ گزارشِ باگ. عکسِ تشخیصیِ سیستم خودکار الصاق می‌شود (فقط دادهٔ فنی).
/// امکانِ الصاقِ اسکرین‌شاتِ خط‌کشی‌شده. ارسال از طریقِ سرورِ پشتیبانی یا صفِ محلی.
/// HC-3b — اگر با <see cref="ExceptionContext"/> باز شود، از استثناء پیش‌پر می‌شود.
/// </summary>
public partial class BugReportViewModel : BaseViewModel, INavigationAware
{
    private readonly IMediator _mediator;
    private readonly DiagnosticsCollector _collector;
    private string? _diagnosticsJson;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string? _expectedResult;
    [ObservableProperty] private string? _actualResult;
    [ObservableProperty] private string? _stepsToReproduce;

    [ObservableProperty] private EnumOption<BugSeverity> _selectedSeverity;
    [ObservableProperty] private EnumOption<SupportCategory> _selectedCategory;

    [ObservableProperty] private string? _attachmentPath;
    [ObservableProperty] private string _attachmentName = "بدونِ پیوست";
    [ObservableProperty] private string _diagnosticsSummary = "در حال جمع‌آوری...";

    [ObservableProperty] private string? _resultMessage;
    [ObservableProperty] private bool _resultIsError;
    [ObservableProperty] private string _screenName = "مرکزِ پشتیبانی";

    public bool HasAttachment => !string.IsNullOrEmpty(AttachmentPath);
    public bool HasResult => !string.IsNullOrEmpty(ResultMessage);
    partial void OnAttachmentPathChanged(string? value) => OnPropertyChanged(nameof(HasAttachment));
    partial void OnResultMessageChanged(string? value) => OnPropertyChanged(nameof(HasResult));

    public ObservableCollection<EnumOption<BugSeverity>> Severities { get; } = new()
    {
        new("کم", BugSeverity.Low), new("متوسط", BugSeverity.Medium),
        new("زیاد", BugSeverity.High), new("بحرانی", BugSeverity.Critical),
    };

    public ObservableCollection<EnumOption<SupportCategory>> Categories { get; } = new()
    {
        new("حسابداری", SupportCategory.Accounting), new("خزانه‌داری", SupportCategory.Treasury),
        new("فروش", SupportCategory.Sales), new("خرید", SupportCategory.Purchase),
        new("انبار", SupportCategory.Inventory), new("گزارش‌ها", SupportCategory.Reports),
        new("امنیت", SupportCategory.Security), new("صندوق فروش (POS)", SupportCategory.Pos),
        new("رستوران", SupportCategory.Restaurant), new("گردشگری", SupportCategory.Tourism),
        new("سیستم", SupportCategory.System), new("سایر", SupportCategory.Other),
    };

    public BugReportViewModel(IMediator mediator, DiagnosticsCollector collector,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _collector = collector;
        _selectedSeverity = Severities[1];   // متوسط
        _selectedCategory = Categories[10];   // سیستم
    }

    public override async Task LoadAsync()
    {
        try
        {
            var info = await _collector.CollectAsync();
            _diagnosticsJson = JsonSerializer.Serialize(info);
            DiagnosticsSummary =
                $"نسخهٔ {info.ErpVersion} · {info.WindowsVersion} · پایگاه‌داده: {info.DatabaseStatus} · " +
                $"لایسنس: {info.LicenseType} · کاربر: {info.User}";
        }
        catch { DiagnosticsSummary = "اطلاعاتِ تشخیصی در دسترس نیست."; }
    }

    /// <summary>🆘 HC-3b — اگر صفحه با یک استثناء باز شود، فرم را از آن پیش‌پر می‌کند.</summary>
    public Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is ExceptionContext ex)
        {
            SelectedCategory = Categories[10];   // سیستم
            SelectedSeverity = Severities[2];    // زیاد
            ScreenName = string.IsNullOrWhiteSpace(ex.Screen) ? ScreenName : ex.Screen;
            Title = "خطای برنامه: " + Trim(ex.Message, 120);
            ActualResult = ex.Message;
            StepsToReproduce = "این گزارش به‌صورتِ خودکار از یک خطای رخ‌داده ساخته شد.";
            Description =
                $"یک خطای مدیریت‌نشده هنگامِ کار با «{ScreenName}» رخ داد.\n\n" +
                $"پیام: {ex.Message}\n\n— Stack Trace —\n{ex.StackTrace}";
        }
        return Task.CompletedTask;
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "…";

    /// <summary>توسطِ View پس از گرفتن/خط‌کشیِ اسکرین‌شات صدا زده می‌شود.</summary>
    public void SetAttachment(string path)
    {
        AttachmentPath = path;
        AttachmentName = System.IO.Path.GetFileName(path);
    }

    [RelayCommand]
    private void RemoveAttachment()
    {
        AttachmentPath = null;
        AttachmentName = "بدونِ پیوست";
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Title)) { await _dialogService.ShowWarningAsync("عنوانِ گزارش را وارد کنید."); return; }
        if (string.IsNullOrWhiteSpace(Description)) { await _dialogService.ShowWarningAsync("شرحِ مشکل را وارد کنید."); return; }

        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new CreateBugReportCommand(
                Title.Trim(), Description.Trim(), SelectedSeverity.Value, SelectedCategory.Value,
                ExpectedResult, ActualResult, StepsToReproduce, _diagnosticsJson, ScreenName, AttachmentPath));

            ResultIsError = !res.Succeeded;
            ResultMessage = res.Succeeded ? res.Value!.Message : res.ErrorMessage;
            if (res.Succeeded) ResetForm();
        }, "در حال ثبتِ گزارش...");
    }

    private void ResetForm()
    {
        Title = string.Empty; Description = string.Empty;
        ExpectedResult = ActualResult = StepsToReproduce = null;
        RemoveAttachment();
    }
}
