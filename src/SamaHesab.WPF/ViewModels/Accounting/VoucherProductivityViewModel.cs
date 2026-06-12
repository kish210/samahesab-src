using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Accounting;

/// <summary>
/// بهره‌وری سند (R5 / #۲۲ و #۲۳): مدیریت الگوهای سند و اسناد تکرارشونده.
/// - ساخت سریع سند پیش‌نویس از الگو.
/// - تعریف سند تکرارشونده روی یک الگو + تولید دستیِ اسناد سررسیدشده.
/// بک‌اند از قبل آماده بود؛ این VM فقط Query/Commandها را صدا می‌زند.
/// </summary>
public partial class VoucherProductivityViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    public ObservableCollection<VoucherTemplateDto> Templates { get; } = new();
    public ObservableCollection<RecurringVoucherDto> Recurring { get; } = new();

    [ObservableProperty] private VoucherTemplateDto? _selectedTemplate;
    [ObservableProperty] private string _createDate = string.Empty;

    // تعریف سند تکرارشونده‌ی جدید
    [ObservableProperty] private string _recurringName = string.Empty;
    [ObservableProperty] private string _recurringStartDate = string.Empty;
    [ObservableProperty] private bool _isYearly;          // پیش‌فرض ماهانه

    public VoucherProductivityViewModel(
        IMediator mediator,
        IPersianCalendarService calendar,
        IDialogService dialogService,
        INavigationService navigationService) : base(dialogService, navigationService)
    {
        _mediator = mediator;
        _calendar = calendar;
    }

    public override async Task LoadAsync()
    {
        CreateDate = _calendar.GetCurrentPersianDate();
        RecurringStartDate = CreateDate;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ExecuteAsync(async () =>
        {
            var tpls = await _mediator.Send(new GetVoucherTemplatesQuery());
            Templates.Clear();
            foreach (var t in tpls) Templates.Add(t);
            SelectedTemplate ??= Templates.FirstOrDefault();

            var recs = await _mediator.Send(new GetRecurringVouchersQuery());
            Recurring.Clear();
            foreach (var r in recs) Recurring.Add(r);
        }, "در حال بارگذاری...");
    }

    /// <summary>ساخت سند پیش‌نویس از الگوی انتخاب‌شده و رفتن به فهرست اسناد.</summary>
    [RelayCommand]
    private async Task CreateFromTemplateAsync()
    {
        if (SelectedTemplate is null)
        { await _dialogService.ShowWarningAsync("یک الگو را انتخاب کنید."); return; }

        await ExecuteAsync(async () =>
        {
            var result = await _mediator.Send(new CreateVoucherFromTemplateCommand(
                SelectedTemplate.Id, CreateDate, SelectedTemplate.Name));

            if (result.Succeeded)
            {
                await _dialogService.ShowSuccessAsync(
                    $"سند پیش‌نویس (شناسه {result.Value}) از الگوی «{SelectedTemplate.Name}» ساخته شد.");
                _navigationService.NavigateTo("Vouchers");
            }
            else
            {
                await _dialogService.ShowErrorAsync(
                    string.IsNullOrEmpty(result.ErrorMessage) ? "ساخت سند از الگو ناموفق بود." : result.ErrorMessage);
            }
        }, "در حال ساخت سند از الگو...");
    }

    /// <summary>تعریف یک سند تکرارشونده روی الگوی انتخاب‌شده.</summary>
    [RelayCommand]
    private async Task SaveRecurringAsync()
    {
        if (SelectedTemplate is null)
        { await _dialogService.ShowWarningAsync("ابتدا الگوی پایه را انتخاب کنید."); return; }
        if (string.IsNullOrWhiteSpace(RecurringName))
        { await _dialogService.ShowWarningAsync("نام سند تکرارشونده را وارد کنید."); return; }

        await ExecuteAsync(async () =>
        {
            var result = await _mediator.Send(new SaveRecurringVoucherCommand(
                SelectedTemplate.Id, RecurringName,
                IsYearly ? RecurrenceFrequency.Yearly : RecurrenceFrequency.Monthly,
                RecurringStartDate));

            if (result.Succeeded)
            {
                await _dialogService.ShowSuccessAsync("سند تکرارشونده تعریف شد.");
                RecurringName = string.Empty;
                await RefreshAsync();
            }
            else
            {
                await _dialogService.ShowErrorAsync(
                    string.IsNullOrEmpty(result.ErrorMessage) ? "تعریف سند تکرارشونده ناموفق بود." : result.ErrorMessage);
            }
        }, "در حال ذخیره...");
    }

    /// <summary>تولید همهٔ اسناد تکرارشونده‌ی سررسیدشده تا امروز (به‌صورت پیش‌نویس).</summary>
    [RelayCommand]
    private async Task GenerateDueAsync()
    {
        if (!await _dialogService.ConfirmAsync(
                "اسناد تکرارشونده‌ی سررسیدشده تا امروز به‌صورت پیش‌نویس ساخته شوند؟"))
            return;

        await ExecuteAsync(async () =>
        {
            var today = _calendar.GetCurrentPersianDate();
            var result = await _mediator.Send(new GenerateDueRecurringVouchersCommand(today));

            if (result.Succeeded)
            {
                var n = result.Value!.Generated;
                await _dialogService.ShowSuccessAsync(
                    n == 0 ? "سند سررسیدشده‌ای برای تولید وجود نداشت."
                           : $"{n} سند پیش‌نویس از اسناد تکرارشونده ساخته شد.");
                await RefreshAsync();
            }
            else
            {
                await _dialogService.ShowErrorAsync(
                    string.IsNullOrEmpty(result.ErrorMessage) ? "تولید اسناد سررسیدشده ناموفق بود." : result.ErrorMessage);
            }
        }, "در حال تولید اسناد سررسیدشده...");
    }
}
