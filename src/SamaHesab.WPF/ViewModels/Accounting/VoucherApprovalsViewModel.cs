using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Accounting;

/// <summary>T22 — کارتابلِ تأییدِ اسناد: فهرستِ اسنادِ «در انتظارِ تأیید» + تأیید/رد.</summary>
public partial class VoucherApprovalsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    public ObservableCollection<PendingApprovalDto> Pending { get; } = new();

    [ObservableProperty] private int _count;
    [ObservableProperty] private PendingApprovalDto? _selected;   // برای میان‌برهای کیبورد

    public VoucherApprovalsViewModel(IMediator mediator,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var rows = await _mediator.Send(new GetPendingApprovalsQuery());
            Pending.Clear();
            foreach (var r in rows) Pending.Add(r);
            Count = Pending.Count;
            Selected = Pending.FirstOrDefault();   // ردیفِ اول برای کارِ سریعِ کیبوردی
        }, "در حال بارگیریِ کارتابلِ تأیید...");
    }

    [RelayCommand] private Task RefreshAsync() => LoadAsync();

    // میان‌برهای کیبورد روی ردیفِ انتخاب‌شده (Enter=تأیید · Del=رد) — کاهشِ کلیک.
    [RelayCommand] private Task ApproveSelectedAsync() => ApproveAsync(Selected);
    [RelayCommand] private Task RejectSelectedAsync() => RejectAsync(Selected);

    [RelayCommand]
    private async Task ApproveAsync(PendingApprovalDto? row)
    {
        if (row is null) return;
        if (!await _dialogService.ConfirmAsync($"تأییدِ سندِ شمارهٔ {row.VoucherNumber}؟")) return;
        await RunAsync(new ApproveVoucherCommand(row.Id), "سند تأیید شد.");
    }

    [RelayCommand]
    private async Task RejectAsync(PendingApprovalDto? row)
    {
        if (row is null) return;
        if (!await _dialogService.ConfirmAsync($"ردِّ سندِ شمارهٔ {row.VoucherNumber}؟")) return;
        await RunAsync(new RejectVoucherCommand(row.Id), "سند رد شد.");
    }

    private async Task RunAsync(IRequest<Application.Common.Models.Result> cmd, string okMsg)
    {
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(cmd);
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync(okMsg);
            await LoadAsync();
        }, "در حال اعمال...");
    }
}
