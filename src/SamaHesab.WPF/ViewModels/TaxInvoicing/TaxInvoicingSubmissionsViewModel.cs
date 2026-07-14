using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Modules.TaxInvoicing.Application.Commands;
using SamaHesab.Modules.TaxInvoicing.Application.Queries;
using SamaHesab.Modules.TaxInvoicing.Domain;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.TaxInvoicing;

/// <summary>
/// فهرستِ صورتحساب‌هایِ الکترونیکیِ ارسال‌شده به سامانهٔ مودیان — هم‌الگو با <c>AuditLogViewModel</c>
/// (فیلتر + بازخوانی) به‌اضافهٔ دکمهٔ «تلاشِ مجدد»یِ ردیفی (الگویِ <c>VoucherApprovalsView</c>).
/// </summary>
public partial class TaxInvoicingSubmissionsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    public ObservableCollection<ElectronicInvoiceSubmissionDto> Rows { get; } = new();

    public record StatusOption(string Display, SubmissionStatus? Value);

    /// <summary>فیلترِ وضعیت — گزینهٔ اولِ «همه» با Value=null.</summary>
    public IReadOnlyList<StatusOption> StatusOptions { get; } = new[]
    {
        new StatusOption("همه", null),
        new StatusOption("در انتظار", SubmissionStatus.Pending),
        new StatusOption("ارسال‌شده", SubmissionStatus.Sent),
        new StatusOption("پذیرفته‌شده", SubmissionStatus.Accepted),
        new StatusOption("ردشده", SubmissionStatus.Rejected),
        new StatusOption("خطا", SubmissionStatus.Error),
    };
    [ObservableProperty] private StatusOption _selectedStatusOption;

    public TaxInvoicingSubmissionsViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _selectedStatusOption = StatusOptions[0]; }

    public override Task LoadAsync() => RunAsync();

    [RelayCommand]
    private async Task RunAsync()
    {
        await ExecuteAsync(async () =>
        {
            var list = await _mediator.Send(new GetElectronicInvoiceSubmissionsQuery(SelectedStatusOption?.Value));
            Rows.Clear();
            foreach (var r in list) Rows.Add(r);
        }, "در حال بارگیریِ فهرست...");
    }

    /// <summary>تلاشِ مجددِ یک ردیفِ مشخص (دکمهٔ روی هر سطر).</summary>
    [RelayCommand]
    private async Task RetryAsync(ElectronicInvoiceSubmissionDto? row)
    {
        if (row is null) return;
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new SendElectronicInvoiceCommand(row.Id));
            if (!res.Succeeded) await _dialogService.ShowErrorAsync(res.ErrorMessage);
            await RunAsync();
        }, "در حال ارسال...");
    }

    /// <summary>تلاشِ مجددِ دسته‌ایِ همهٔ رکوردهایِ Pending/Error — دستی، نه خودکار (طبقِ پلن).</summary>
    [RelayCommand]
    private async Task RetryAllPendingAsync()
    {
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new RetryPendingElectronicInvoicesCommand());
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync(
                $"از {res.Value!.Attempted} موردِ صف‌شده، {res.Value.Succeeded} موفق و {res.Value.Failed} ناموفق بود.");
            await RunAsync();
        }, "در حال ارسالِ دسته‌ای...");
    }
}
