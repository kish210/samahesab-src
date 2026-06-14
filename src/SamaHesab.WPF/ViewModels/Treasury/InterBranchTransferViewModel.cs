using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Settings.Commands;
using SamaHesab.Application.Treasury.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Treasury;

/// <summary>
/// MB-1 گام۴ — سندِ تسویهٔ بین‌شعبه: انتقالِ وجه از یک شعبه به شعبهٔ دیگر.
/// دو سندِ قطعیِ متوازن (یکی per شعبه) از طریق `CreateInterBranchTransferCommand` ساخته می‌شود،
/// با «حساب جاریِ فی‌مابینِ شعب» (پیش‌فرض کد 1-07-001) به‌عنوان واسط.
/// </summary>
public partial class InterBranchTransferViewModel : BaseViewModel
{
    private const string InterBranchCode = "1-07-001";

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _user;
    private readonly IPersianCalendarService _calendar;

    public ObservableCollection<BranchDto> Branches { get; } = new();
    public ObservableCollection<AccountDto> Accounts { get; } = new();

    [ObservableProperty] private int? _selectedFromBranchId;
    [ObservableProperty] private int? _selectedToBranchId;
    [ObservableProperty] private int? _selectedFromAccountId;
    [ObservableProperty] private int? _selectedToAccountId;
    [ObservableProperty] private int? _selectedInterBranchAccountId;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private string _date = string.Empty;
    [ObservableProperty] private string? _description;

    public InterBranchTransferViewModel(IMediator mediator, ICurrentUserService user,
        IPersianCalendarService calendar, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _user = user; _calendar = calendar;
    }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            Date = _calendar.GetCurrentPersianDate();

            var branches = await _mediator.Send(new GetBranchesQuery());
            Branches.Clear(); foreach (var b in branches) Branches.Add(b);

            var accounts = await _mediator.Send(new GetAccountsQuery(LeafOnly: true));
            Accounts.Clear(); foreach (var a in accounts) Accounts.Add(a);

            // پیش‌فرضِ حسابِ واسط = حساب جاریِ فی‌مابینِ شعب.
            SelectedInterBranchAccountId = Accounts.FirstOrDefault(a => a.Code == InterBranchCode)?.Id;
        }, "در حال بارگیری شعب و حساب‌ها...");
    }

    [RelayCommand]
    private async Task TransferAsync()
    {
        if (SelectedFromBranchId is not int from || SelectedToBranchId is not int to)
        { await _dialogService.ShowErrorAsync("شعبهٔ مبدأ و مقصد را انتخاب کنید."); return; }
        if (from == to)
        { await _dialogService.ShowErrorAsync("شعبهٔ مبدأ و مقصد نمی‌توانند یکسان باشند."); return; }
        if (SelectedFromAccountId is not int fromAcc || SelectedToAccountId is not int toAcc)
        { await _dialogService.ShowErrorAsync("حسابِ صندوق/بانکِ مبدأ و مقصد را انتخاب کنید."); return; }
        if (Amount <= 0)
        { await _dialogService.ShowErrorAsync("مبلغ باید بزرگتر از صفر باشد."); return; }

        var fromName = Branches.FirstOrDefault(b => b.Id == from)?.Name ?? "مبدأ";
        var toName = Branches.FirstOrDefault(b => b.Id == to)?.Name ?? "مقصد";
        if (!await _dialogService.ConfirmAsync($"انتقالِ {Amount:#,##0} ریال از «{fromName}» به «{toName}»؟")) return;

        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new CreateInterBranchTransferCommand(
                from, to, 1, Date, Amount, fromAcc, toAcc, SelectedInterBranchAccountId, Description));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }

            await _dialogService.ShowSuccessAsync(
                $"انتقال ثبت شد. سندِ مبدأ #{res.Value!.FromVoucherId} و سندِ مقصد #{res.Value.ToVoucherId} (مرجع {res.Value.Reference}).");
            Amount = 0; Description = null;
        }, "در حال ثبتِ سندِ تسویهٔ بین‌شعبه...");
    }
}
