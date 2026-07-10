using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Application.Treasury.Commands;
using SamaHesab.Application.Treasury.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Treasury;

/// <summary>
/// CORE-UX-NAV (pc) — صفحهٔ مستقلِ «دریافت و پرداختِ وجه»: فرمِ دومنظوره (دریافت از مشتری /
/// پرداخت به تأمین‌کننده) با تخصیصِ خودکارِ FIFO + سندِ خودکار، و فهرستِ اخیرِ دریافت/پرداخت.
/// </summary>
public partial class ReceivePaymentViewModel : BaseViewModel, INavigationAware
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _user;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private bool _isReceive = true;   // true=دریافت · false=پرداخت
    [ObservableProperty] private int _selectedPartyId;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private string _paymentMethod = "نقدی";
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _filter = string.Empty;

    public ObservableCollection<PersonDto> Parties { get; } = new();
    public ObservableCollection<ReceiptPaymentRow> Recent { get; } = new();
    public string[] Methods { get; } = { "نقدی", "بانک", "چک" };

    private readonly System.Collections.Generic.List<PersonDto> _allParties = new();

    public string ActionTitle => IsReceive ? "دریافتِ وجه از مشتری" : "پرداختِ وجه به تأمین‌کننده";
    partial void OnIsReceiveChanged(bool v) => OnPropertyChanged(nameof(ActionTitle));
    partial void OnFilterChanged(string v) => ApplyFilter();

    public ReceivePaymentViewModel(IMediator mediator, ICurrentUserService user,
        IPersianCalendarService calendar, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _user = user; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            _allParties.Clear();
            foreach (var p in await _mediator.Send(new GetPersonsQuery())) _allParties.Add(p);
            ApplyFilter();
            await RefreshRecentAsync();
        }, "در حال بارگذاری...");
    }

    private void ApplyFilter()
    {
        var t = (Filter ?? "").Trim();
        Parties.Clear();
        foreach (var p in _allParties.Where(p => t.Length == 0 || p.Name.Contains(t) || (p.Code?.Contains(t) ?? false)))
            Parties.Add(p);
    }

    private async Task RefreshRecentAsync()
    {
        Recent.Clear();
        foreach (var r in await _mediator.Send(new GetReceiptsPaymentsQuery(1))) Recent.Add(r);
    }

    [RelayCommand] private void SetReceive() => IsReceive = true;
    [RelayCommand] private void SetPay() => IsReceive = false;
    [RelayCommand] private Task RefreshAsync() => LoadAsync();

    /// <summary>UX — پیش‌انتخابِ طرف‌حساب/حالتِ دریافت‌یاپرداخت وقتی از دکمهٔ کارتِ شخص باز می‌شود.</summary>
    public Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is PreselectPartyParam p)
        {
            IsReceive = p.IsReceive;
            SelectedPartyId = p.PartyId;
        }
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (SelectedPartyId <= 0) { await _dialogService.ShowWarningAsync("طرف‌حساب را انتخاب کنید."); return; }
        if (Amount <= 0) { await _dialogService.ShowWarningAsync("مبلغ باید بزرگ‌تر از صفر باشد."); return; }
        var name = _allParties.FirstOrDefault(p => p.Id == SelectedPartyId)?.Name ?? "طرف‌حساب";
        var verb = IsReceive ? "دریافت" : "پرداخت";
        var prep = IsReceive ? "از" : "به";

        // AUDIT-3 — هشدارِ غیرمسدودکننده: پرداختِ نقدی که موجودیِ صندوق را منفی می‌کند (صندوق فیزیکی
        // نباید زیرِ صفر برود). فقط هشدار است؛ کاربر می‌تواند ادامه دهد (مثلاً اصلاحِ ماندهٔ اولیه).
        var warn = string.Empty;
        if (!IsReceive && PaymentMethod == "نقدی")
        {
            var cash = await _mediator.Send(new SamaHesab.Application.Treasury.Queries.GetAccountBalanceQuery("1-01-001"));
            if (cash < Amount)
                warn = $"\n\n⚠ موجودیِ صندوق {cash:#,##0} ریال است — این پرداخت آن را منفی می‌کند.";
        }
        if (!await _dialogService.ConfirmAsync($"ثبتِ {verb} {Amount:#,##0} ریال {prep} «{name}»؟{warn}")) return;

        await ExecuteAsync(async () =>
        {
            var branch = _user.BranchId ?? 1;
            var date = _calendar.GetCurrentPersianDate();
            var desc = string.IsNullOrWhiteSpace(Description) ? $"{verb} {prep} {name}" : Description.Trim();
            Result<int> res = IsReceive
                ? await _mediator.Send(new CreateReceiptCommand(branch, 1, date, SelectedPartyId, Amount, PaymentMethod, desc))
                : await _mediator.Send(new CreatePaymentCommand(branch, 1, date, SelectedPartyId, Amount, PaymentMethod, desc));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync($"{verb} ثبت شد (سند #{res.Value}).");
            Amount = 0; Description = string.Empty;
            await RefreshRecentAsync();
        }, $"در حال ثبتِ {verb}...");
    }
}

/// <summary>پارامترِ ناوبری برایِ بازکردنِ این صفحه با طرف‌حسابِ از‌پیش‌انتخاب‌شده (مثلاً از کارتِ شخص).</summary>
public record PreselectPartyParam(int PartyId, bool IsReceive = true);
